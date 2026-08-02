using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "IHSA,NEDCAdmin,CTSHIPAdmin,Admin")]
public class IHSAController : Controller
{
    private const string ReferralProviderLevel = "Referral Hospital";
    private readonly ApplicationDbContext _context;
    private readonly IMonitoringIndicatorService _monitoringIndicatorService;
    private readonly IReferralService _referralService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppNotificationService _notificationService;
    private readonly IAuditService _auditService;

    public IHSAController(
        ApplicationDbContext context,
        IMonitoringIndicatorService monitoringIndicatorService,
        IReferralService referralService,
        UserManager<ApplicationUser> userManager,
        IAppNotificationService notificationService,
        IAuditService auditService)
    {
        _context = context;
        _monitoringIndicatorService = monitoringIndicatorService;
        _referralService = referralService;
        _userManager = userManager;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    public async Task<IActionResult> Dashboard(
        string? state,
        int? hmoId,
        CancellationToken cancellationToken)
    {
        MonitoringDashboardViewModel model = await _monitoringIndicatorService.BuildDashboardAsync(
            state,
            null,
            hmoId,
            cancellationToken);

        ViewBag.AvailableHmos = await _context.Hmos
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        ViewBag.SelectedHmoId = model.SelectedHmoId;
        return View(model);
    }

    public async Task<IActionResult> Referrals(
        string? search,
        string status = "All",
        CancellationToken cancellationToken = default)
    {
        List<ReferralIndexViewModel> referrals = await _referralService.GetHmoReferralsAsync(null, search, cancellationToken);
        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse(status, out ReferralStatus selectedStatus))
        {
            referrals = referrals.Where(x => x.Status == selectedStatus).ToList();
        }

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.TotalReferrals = referrals.Count;
        ViewBag.PendingReferrals = referrals.Count(x => x.Status is ReferralStatus.SubmittedToHmo or ReferralStatus.Verified or ReferralStatus.Received);
        ViewBag.CompletedReferrals = referrals.Count(x => x.Status is ReferralStatus.Closed or ReferralStatus.Audited);
        return View(referrals);
    }

    public async Task<IActionResult> ReferralDetails(Guid id, CancellationToken cancellationToken)
    {
        ReferralDetailsViewModel? referral = await _referralService.GetReferralDetailsAsync(id, cancellationToken);
        if (referral == null)
        {
            return NotFound();
        }

        ViewBag.EnrolleeId = await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.EnrollmentNumber == referral.EnrolleeNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return View(referral);
    }

    public async Task<IActionResult> MonthlyReports(
        string? reportingPeriod,
        string? state,
        CancellationToken cancellationToken)
    {
        IQueryable<StateOfficeMonthlyReport> query = ReportQuery(isReferralProviderReport: false);
        ApplyReportFilters(ref query, reportingPeriod, state);
        ViewBag.ReportingPeriod = reportingPeriod;
        ViewBag.State = state;
        ViewBag.AvailableStates = await GetAvailableStatesAsync(false, cancellationToken);
        return View(await query.OrderByDescending(x => x.ReportingMonth).ThenByDescending(x => x.DateSubmitted).ToListAsync(cancellationToken));
    }

    public async Task<IActionResult> ReferralProviderReports(
        string? reportingPeriod,
        string? state,
        CancellationToken cancellationToken)
    {
        IQueryable<StateOfficeMonthlyReport> query = ReportQuery(isReferralProviderReport: true);
        ApplyReportFilters(ref query, reportingPeriod, state);
        ViewBag.ReportingPeriod = reportingPeriod;
        ViewBag.State = state;
        ViewBag.AvailableStates = await GetAvailableStatesAsync(true, cancellationToken);
        return View(await query.OrderByDescending(x => x.ReportingMonth).ThenByDescending(x => x.DateSubmitted).ToListAsync(cancellationToken));
    }

    public async Task<IActionResult> MonthlyReportDetails(int id, CancellationToken cancellationToken)
    {
        StateOfficeMonthlyReport? report = await ReportQuery(false).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return report == null ? NotFound() : View(report);
    }

    public async Task<IActionResult> ReferralProviderReportDetails(int id, CancellationToken cancellationToken)
    {
        StateOfficeMonthlyReport? report = await ReportQuery(true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return report == null ? NotFound() : View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuditMonthlyReport(int id, string auditStatus, string? auditNote, CancellationToken cancellationToken)
    {
        return await AuditReportAsync(id, auditStatus, auditNote, false, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuditReferralProviderReport(int id, string auditStatus, string? auditNote, CancellationToken cancellationToken)
    {
        return await AuditReportAsync(id, auditStatus, auditNote, true, cancellationToken);
    }

    private async Task<IActionResult> AuditReportAsync(
        int id,
        string auditStatus,
        string? auditNote,
        bool isReferralProviderReport,
        CancellationToken cancellationToken)
    {
        if (auditStatus is not "Audited" and not "Needs Correction")
        {
            TempData["Error"] = "Select a valid audit decision.";
            return RedirectToReportDetails(id, isReferralProviderReport);
        }

        StateOfficeMonthlyReport? report = await _context.StateOfficeMonthlyReports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report == null || !await IsReportTypeAsync(report.ProviderId, isReferralProviderReport, cancellationToken))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        report.AuditStatus = auditStatus;
        report.AuditNote = auditNote?.Trim();
        report.AuditedAt = DateTime.UtcNow;
        report.AuditedByUserId = user.Id;
        report.AuditedByName = user.FullName ?? user.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        await _notificationService.NotifyMonthlyReportAuditedAsync(report.Id, isReferralProviderReport, cancellationToken);
        await _auditService.LogAsync(
            isReferralProviderReport ? "IHSA.ReferralProviderReportAudited" : "IHSA.MonthlyReportAudited",
            AuditActor.Format(user, User.Identity?.Name),
            report.FacilityCode,
            AuditActor.Details(
                $"State:{report.State}",
                $"Facility:{report.FacilityName}",
                $"Month:{report.ReportingMonth:yyyy-MM}",
                $"Status:{report.AuditStatus}",
                $"Note:{report.AuditNote}"),
            cancellationToken);

        TempData["Success"] = "Report audit decision saved.";
        return RedirectToReportDetails(id, isReferralProviderReport);
    }

    private RedirectToActionResult RedirectToReportDetails(int id, bool isReferralProviderReport)
    {
        return isReferralProviderReport
            ? RedirectToAction(nameof(ReferralProviderReportDetails), new { id })
            : RedirectToAction(nameof(MonthlyReportDetails), new { id });
    }

    private IQueryable<StateOfficeMonthlyReport> ReportQuery(bool isReferralProviderReport)
    {
        IQueryable<StateOfficeMonthlyReport> query = _context.StateOfficeMonthlyReports.AsNoTracking();
        return isReferralProviderReport
            ? query.Where(report => _context.Providers.Any(provider =>
                provider.Id == report.ProviderId &&
                (provider.Level == ReferralProviderLevel || provider.Code.StartsWith("REF-"))))
            : query.Where(report => !_context.Providers.Any(provider =>
                provider.Id == report.ProviderId &&
                (provider.Level == ReferralProviderLevel || provider.Code.StartsWith("REF-"))));
    }

    private async Task<bool> IsReportTypeAsync(int providerId, bool isReferralProviderReport, CancellationToken cancellationToken)
    {
        bool isReferralProvider = await _context.Providers
            .AsNoTracking()
            .AnyAsync(provider => provider.Id == providerId &&
                (provider.Level == ReferralProviderLevel || provider.Code.StartsWith("REF-")), cancellationToken);
        return isReferralProvider == isReferralProviderReport;
    }

    private static void ApplyReportFilters(
        ref IQueryable<StateOfficeMonthlyReport> query,
        string? reportingPeriod,
        string? state)
    {
        if (!string.IsNullOrWhiteSpace(state))
        {
            string selectedState = state.Trim();
            query = query.Where(x => x.State == selectedState);
        }

        if (DateTime.TryParseExact(
            $"{reportingPeriod}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime month))
        {
            query = query.Where(x => x.ReportingMonth == month.Date);
        }
    }

    private async Task<List<string>> GetAvailableStatesAsync(bool referralProviderReports, CancellationToken cancellationToken)
    {
        IQueryable<Provider> providers = _context.Providers.AsNoTracking();
        providers = referralProviderReports
            ? providers.Where(provider => provider.Level == ReferralProviderLevel || provider.Code.StartsWith("REF-"))
            : providers.Where(provider => provider.Level != ReferralProviderLevel && !provider.Code.StartsWith("REF-"));

        return await providers
            .Where(provider => provider.State != "")
            .Select(provider => provider.State)
            .Distinct()
            .OrderBy(state => state)
            .ToListAsync(cancellationToken);
    }
}


