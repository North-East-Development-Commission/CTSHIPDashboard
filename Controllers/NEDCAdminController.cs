using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "NEDCAdmin")]
public class NEDCAdminController : Controller
{
    private const string ReferralProviderLevel = "Referral Hospital";
    private readonly ApplicationDbContext _context;
    private readonly IMonitoringIndicatorService _monitoringIndicatorService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppNotificationService _notificationService;
    private readonly IAuditService _auditService;

    public NEDCAdminController(
        ApplicationDbContext context,
        IMonitoringIndicatorService monitoringIndicatorService,
        UserManager<ApplicationUser> userManager,
        IAppNotificationService notificationService,
        IAuditService auditService)
    {
        _context = context;
        _monitoringIndicatorService = monitoringIndicatorService;
        _userManager = userManager;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    public async Task<IActionResult> Dashboard(string? state, int? hmoId, CancellationToken cancellationToken)
    {
        MonitoringDashboardViewModel model = await _monitoringIndicatorService.BuildDashboardAsync(
            state, null, hmoId, cancellationToken);

        ViewBag.AvailableHmos = await _context.Hmos.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        ViewBag.SelectedHmoId = model.SelectedHmoId;
        ViewBag.DashboardTitle = "NEDC HQ Oversight";
        ViewBag.DashboardHeading = "NEDC HQ Oversight Dashboard";
        ViewBag.DashboardDescription = "Executive oversight of CTSHIP delivery, IHSA-reviewed reports, finance, referrals, and programme performance.";
        ViewBag.DashboardViewLabel = "NEDC administrative control view";
        ViewBag.IsNedcDashboard = true;
        ViewBag.NedcPendingReports = await _context.StateOfficeMonthlyReports
            .AsNoTracking()
            .CountAsync(x => x.AuditStatus == "Audited" && x.NedcAuditStatus == "Pending", cancellationToken);
        ViewBag.NedcCompletedReports = await _context.StateOfficeMonthlyReports
            .AsNoTracking()
            .CountAsync(x => x.AuditStatus == "Audited" && x.NedcAuditStatus != "Pending", cancellationToken);

        return View("~/Views/IHSA/Dashboard.cshtml", model);
    }

    public Task<IActionResult> MonthlyReports(
        string? reportingPeriod, string? state, string nedcStatus = "All",
        CancellationToken cancellationToken = default) =>
        ReportsAsync(false, reportingPeriod, state, nedcStatus, cancellationToken);

    public Task<IActionResult> ReferralProviderReports(
        string? reportingPeriod, string? state, string nedcStatus = "All",
        CancellationToken cancellationToken = default) =>
        ReportsAsync(true, reportingPeriod, state, nedcStatus, cancellationToken);

    public Task<IActionResult> MonthlyReportDetails(int id, CancellationToken cancellationToken) =>
        ReportDetailsAsync(id, false, cancellationToken);

    public Task<IActionResult> ReferralProviderReportDetails(int id, CancellationToken cancellationToken) =>
        ReportDetailsAsync(id, true, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AuditMonthlyReport(
        int id, string auditStatus, string? auditNote, CancellationToken cancellationToken) =>
        AuditReportAsync(id, auditStatus, auditNote, false, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AuditReferralProviderReport(
        int id, string auditStatus, string? auditNote, CancellationToken cancellationToken) =>
        AuditReportAsync(id, auditStatus, auditNote, true, cancellationToken);

    private async Task<IActionResult> ReportsAsync(
        bool isReferralProviderReport,
        string? reportingPeriod,
        string? state,
        string nedcStatus,
        CancellationToken cancellationToken)
    {
        IQueryable<StateOfficeMonthlyReport> query = ReportQuery(isReferralProviderReport)
            .Where(x => x.AuditStatus == "Audited");

        if (!string.IsNullOrWhiteSpace(state))
        {
            string selectedState = state.Trim();
            query = query.Where(x => x.State == selectedState);
        }

        if (DateTime.TryParseExact(
            $"{reportingPeriod}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime month))
        {
            query = query.Where(x => x.ReportingMonth == month.Date);
        }

        if (nedcStatus is "Pending" or "Approved" or "Returned")
        {
            query = query.Where(x => x.NedcAuditStatus == nedcStatus);
        }
        else
        {
            nedcStatus = "All";
        }

        IQueryable<Provider> providers = _context.Providers.AsNoTracking();
        providers = isReferralProviderReport
            ? providers.Where(IsReferralProvider())
            : providers.Where(x => x.Level != ReferralProviderLevel && !x.Code.StartsWith("REF-"));

        ViewBag.IsReferralProviderReport = isReferralProviderReport;
        ViewBag.ReportingPeriod = reportingPeriod;
        ViewBag.State = state;
        ViewBag.NedcStatus = nedcStatus;
        ViewBag.AvailableStates = await providers
            .Where(x => x.State != "")
            .Select(x => x.State)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        List<StateOfficeMonthlyReport> reports = await query
            .OrderBy(x => x.NedcAuditStatus == "Pending" ? 0 : 1)
            .ThenByDescending(x => x.AuditedAt)
            .ToListAsync(cancellationToken);
        return View("~/Views/NEDCAdmin/Reports.cshtml", reports);
    }

    private async Task<IActionResult> ReportDetailsAsync(
        int id, bool isReferralProviderReport, CancellationToken cancellationToken)
    {
        StateOfficeMonthlyReport? report = await ReportQuery(isReferralProviderReport)
            .FirstOrDefaultAsync(x => x.Id == id && x.AuditStatus == "Audited", cancellationToken);
        if (report == null)
        {
            return NotFound();
        }

        ViewBag.IsReferralProviderReport = isReferralProviderReport;
        return View("~/Views/NEDCAdmin/ReportDetails.cshtml", report);
    }

    private async Task<IActionResult> AuditReportAsync(
        int id,
        string auditStatus,
        string? auditNote,
        bool isReferralProviderReport,
        CancellationToken cancellationToken)
    {
        if (auditStatus is not "Approved" and not "Returned")
        {
            TempData["Error"] = "Select a valid NEDC HQ audit decision.";
            return RedirectToDetails(id, isReferralProviderReport);
        }

        StateOfficeMonthlyReport? report = await _context.StateOfficeMonthlyReports
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report == null ||
            report.AuditStatus != "Audited" ||
            !await IsReportTypeAsync(report.ProviderId, isReferralProviderReport, cancellationToken))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        report.NedcAuditStatus = auditStatus;
        report.NedcAuditNote = auditNote?.Trim();
        report.NedcAuditedAt = DateTime.UtcNow;
        report.NedcAuditedByUserId = user.Id;
        report.NedcAuditedByName = user.FullName ?? user.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        await _notificationService.NotifyMonthlyReportNedcAuditedAsync(
            report.Id, isReferralProviderReport, cancellationToken);
        await _auditService.LogAsync(
            isReferralProviderReport ? "NEDCAdmin.ReferralProviderReportAudited" : "NEDCAdmin.MonthlyReportAudited",
            AuditActor.Format(user, User.Identity?.Name),
            report.FacilityCode,
            AuditActor.Details(
                $"State:{report.State}",
                $"Facility:{report.FacilityName}",
                $"Month:{report.ReportingMonth:yyyy-MM}",
                $"IHSAStatus:{report.AuditStatus}",
                $"NEDCStatus:{report.NedcAuditStatus}",
                $"Note:{report.NedcAuditNote}"),
            cancellationToken);

        TempData["Success"] = "NEDC HQ audit decision saved and returned to IHSA.";
        return RedirectToDetails(id, isReferralProviderReport);
    }

    private RedirectToActionResult RedirectToDetails(int id, bool isReferralProviderReport) =>
        isReferralProviderReport
            ? RedirectToAction(nameof(ReferralProviderReportDetails), new { id })
            : RedirectToAction(nameof(MonthlyReportDetails), new { id });

    private IQueryable<StateOfficeMonthlyReport> ReportQuery(bool isReferralProviderReport)
    {
        IQueryable<StateOfficeMonthlyReport> query = _context.StateOfficeMonthlyReports.AsNoTracking();
        return isReferralProviderReport
            ? query.Where(x => _context.Providers.Any(p => p.Id == x.ProviderId &&
                (p.Level == ReferralProviderLevel || p.Code.StartsWith("REF-"))))
            : query.Where(x => !_context.Providers.Any(p => p.Id == x.ProviderId &&
                (p.Level == ReferralProviderLevel || p.Code.StartsWith("REF-"))));
    }

    private async Task<bool> IsReportTypeAsync(
        int providerId, bool isReferralProviderReport, CancellationToken cancellationToken)
    {
        bool isReferralProvider = await _context.Providers.AsNoTracking()
            .AnyAsync(p => p.Id == providerId &&
                (p.Level == ReferralProviderLevel || p.Code.StartsWith("REF-")), cancellationToken);
        return isReferralProvider == isReferralProviderReport;
    }

    private static System.Linq.Expressions.Expression<Func<Provider, bool>> IsReferralProvider() =>
        x => x.Level == ReferralProviderLevel || x.Code.StartsWith("REF-");
}