using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Globalization;


[Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin,HMO")]
public class StateOfficeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMonitoringIndicatorService _monitoringIndicatorService;
    private readonly IAuditService _auditService;

    public StateOfficeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMonitoringIndicatorService monitoringIndicatorService,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _monitoringIndicatorService = monitoringIndicatorService;
        _auditService = auditService;
    }

    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(user.State))
        {
            return Forbid();
        }

        var state = user.State;

        var vm = new StateOfficeDashboardViewModel
        {
            StateName = state,
            TotalEnrollees = await _context.Enrollees.CountAsync(e => e.State == state),
            TotalProviders = await _context.Providers.CountAsync(e => e.State == state),
            ActiveEnrollees = await _context.Enrollees.CountAsync(e => e.State == state && e.Status == "Active"),
            TotalClaims = await _context.Claims
                .Include(c => c.Enrollee)
                .Where(c => c.Enrollee != null && c.Enrollee.State == state)
                .CountAsync(),
            PaidClaims = await _context.Claims
                .Include(c => c.Enrollee)
                .Where(c => c.Enrollee != null && c.Enrollee.State == state && c.Status == "Paid")
                .CountAsync(),
            HmoCount = await _context.Hmos
                .Where(h => h.Enrollees.Any(e => e.State == state))
                .Select(h => h.Id)
                .Distinct()
                .CountAsync(),
            Monitoring = await _monitoringIndicatorService.BuildDashboardAsync(state),
            ComplaintMetrics = await ComplaintMetricsService.BuildAsync(
                _context.Complaints.Where(complaint => complaint.State == state)),
            
            RecentEnrollees = await _context.Enrollees
                .Where(e => e.State == state)
                .OrderByDescending(e => e.DateRegistered)
                .Take(10)
                .Select(e => new EnrolleeSummaryViewModel
                {
                    Id = e.Id,
                    FullName = e.FullName,
                    EnrollmentNumber = e.EnrollmentNumber,
                    HmoName = e.Hmo != null ? e.Hmo.Name : "Not Assigned",
                    DateRegistered = e.DateRegistered,
                    Status = e.Status
                })
                .ToListAsync()
        };

        return View(vm);
    }

    // ====================== ENROLLEES LIST ======================
    [Authorize(Roles = "CTSHIPAdmin,StateOffice")]
    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> Enrollees(string state = "", string search = "", int page = 1, int pageSize = 25)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(user.State))
        {
            return Forbid();
        }

        // If no state is passed, use the user's assigned state (for State Officers)
        if (string.IsNullOrWhiteSpace(state))
        {
            state = user.State;
        }

        // Security: State Officers can only view their own state (Admin can view any)
        bool isAdmin = User.IsInRole("CTSHIPAdmin");
        if (!isAdmin && state != user.State)
        {
            TempData["Error"] = "You are not authorized to view this state's data.";
            return RedirectToAction(nameof(Index));
        }

        var query = _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => e.State == state);

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.FullName, s) ||
                EF.Functions.Like(e.EnrollmentNumber, s) ||
                EF.Functions.Like(e.Phone ?? "", s) ||
                EF.Functions.Like(e.NIN.ToString(), s));
        }

        var totalItems = await query.CountAsync();

        var enrollees = await query
            .OrderByDescending(e => e.DateRegistered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.EnrollmentNumber,
                e.FullName,
                e.Phone,
                e.NIN,
                e.State,
                e.LGA,
                HmoName = e.Hmo != null ? e.Hmo.Name : "N/A",
                e.DateRegistered,
                Status = e.Status ?? "Active"
            })
            .ToListAsync();

        ViewBag.State = state;
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalEnrollees = totalItems;
        ViewBag.UserState = user.State;   // Useful for view logic

        return View(enrollees);
    }

    // ====================== PROVIDERS LIST ======================
    [Authorize(Roles = "CTSHIPAdmin,StateOffice")]
    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> Providers(string state = "", string search = "", int page = 1, int pageSize = 20)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(user.State))
            return Forbid();

        if (string.IsNullOrWhiteSpace(state))
            state = user.State;

        // Security: State Officers can only view their own state
        bool isAdmin = User.IsInRole("CTSHIPAdmin");
        if (!isAdmin && state != user.State)
        {
            TempData["Error"] = "You are not authorized to view this state's data.";
            return RedirectToAction(nameof(Index));
        }

        var query = _context.Providers.Where(p => p.State == state);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Name, s) ||
                EF.Functions.Like(p.Code, s) ||
                EF.Functions.Like(p.Location, s));
        }

        var totalItems = await query.CountAsync();

        var providers = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Level,
                p.Location,
                p.Phone,
                p.Email,
                p.IsActive
            })
            .ToListAsync();

        ViewBag.State = state;
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalProviders = totalItems;

        return View(providers);
    }

    [Authorize(Roles = "CTSHIPAdmin,StateOffice")]
    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> Claims(
        string state = "",
        string status = "All",
        string search = "",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        if (!User.IsInRole("CTSHIPAdmin"))
        {
            if (string.IsNullOrWhiteSpace(user.State))
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(state)
                && !string.Equals(state, user.State, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "You are not authorized to view this state's claims.";
                return RedirectToAction(nameof(Index));
            }

            state = user.State;
        }
        else if (string.IsNullOrWhiteSpace(state))
        {
            state = !string.IsNullOrWhiteSpace(user.State)
                ? user.State
                : await GetDefaultStateAsync(cancellationToken) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return Forbid();
        }

        state = state.Trim();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        status = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
        search = search?.Trim() ?? string.Empty;

        string[] pendingStatuses = { "Submitted", "ReApproved" };
        string[] approvedStatuses = { "Approved", "Review Approved" };

        IQueryable<Claim> stateClaims = _context.Claims
            .AsNoTracking()
            .Where(c => c.Enrollee != null && c.Enrollee.State == state);

        IQueryable<Claim> filteredClaims = stateClaims;

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredClaims = filteredClaims.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = $"%{search}%";
            filteredClaims = filteredClaims.Where(c =>
                EF.Functions.Like(c.ClaimNumber, s)
                || (c.Enrollee != null && EF.Functions.Like(c.Enrollee.FullName, s))
                || (c.Enrollee != null && EF.Functions.Like(c.Enrollee.EnrollmentNumber, s))
                || (c.Enrollee != null && c.Enrollee.Hmo != null && EF.Functions.Like(c.Enrollee.Hmo.Name, s))
                || (c.Hmos != null && EF.Functions.Like(c.Hmos.Name, s))
                || (c.Provider != null && EF.Functions.Like(c.Provider.Name, s))
                || EF.Functions.Like(c.Diagnosis, s));
        }

        int totalFilteredClaims = await filteredClaims.CountAsync(cancellationToken);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredClaims / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var model = new StateOfficeClaimsViewModel
        {
            StateName = state,
            Search = search,
            Status = status,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            TotalFilteredClaims = totalFilteredClaims,
            TotalClaims = await stateClaims.CountAsync(cancellationToken),
            PendingClaims = await stateClaims.CountAsync(
                c => pendingStatuses.Contains(c.Status),
                cancellationToken),
            ApprovedClaims = await stateClaims.CountAsync(
                c => approvedStatuses.Contains(c.Status),
                cancellationToken),
            PaidClaims = await stateClaims.CountAsync(c => c.Status == "Paid", cancellationToken),
            RejectedClaims = await stateClaims.CountAsync(c => c.Status == "Rejected", cancellationToken),
            TotalClaimValue = await stateClaims.SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0m,
            PendingClaimValue = await stateClaims
                .Where(c => pendingStatuses.Contains(c.Status) || approvedStatuses.Contains(c.Status))
                .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0m,
            PaidClaimValue = await stateClaims
                .Where(c => c.Status == "Paid")
                .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0m,
            AvailableStates = await GetAvailableStatesAsync(cancellationToken),
            Claims = await filteredClaims
                .OrderByDescending(c => c.DateSubmitted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new StateOfficeClaimRowViewModel
                {
                    Id = c.Id,
                    ClaimNumber = c.ClaimNumber,
                    EnrolleeName = c.Enrollee != null ? c.Enrollee.FullName : "Not Available",
                    EnrollmentNumber = c.Enrollee != null ? c.Enrollee.EnrollmentNumber : "N/A",
                    HmoName = c.Enrollee != null && c.Enrollee.Hmo != null
                        ? c.Enrollee.Hmo.Name
                        : c.Hmos != null ? c.Hmos.Name : "Not Assigned",
                    ProviderName = c.Provider != null ? c.Provider.Name : "Not Assigned",
                    Diagnosis = c.Diagnosis,
                    Amount = c.Amount,
                    Status = c.Status,
                    DateSubmitted = c.DateSubmitted,
                    DatePaid = c.DatePaid
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [Authorize(Roles = "CTSHIPAdmin,StateOffice")]
    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> ClaimDetails(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        Claim? claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.Enrollee)
                .ThenInclude(e => e!.Hmo)
            .Include(c => c.Hmos)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (claim == null)
        {
            return NotFound();
        }

        string claimState = claim.Enrollee?.State ?? string.Empty;
        if (!User.IsInRole("CTSHIPAdmin"))
        {
            if (string.IsNullOrWhiteSpace(user.State)
                || !string.Equals(claimState, user.State, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }
        }

        return View(new StateOfficeClaimDetailsViewModel
        {
            StateName = claimState,
            Claim = claim
        });
    }


    [Authorize(Roles = "StateOffice,CTSHIPAdmin,Admin")]
    public async Task<IActionResult> ExportEnrollees(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "Lagos";

        var enrollees = await _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => e.State == state)
            .Select(e => new
            {
                EnrollmentNumber = e.EnrollmentNumber,
                FullName = e.FullName,
                NIN = e.NIN,
                Phone = e.Phone,
                Gender = e.Gender,
                DateOfBirth = e.DateOfBirth,
                State = e.State,
                LGA = e.LGA,
                Ward = e.Ward,
                HmoName = e.Hmo != null ? e.Hmo.Name : "N/A",
                Status = e.Status ?? "Active",
                DateRegistered = e.DateRegistered
            })
            .OrderBy(e => e.FullName)
            .ToListAsync();

        if (!enrollees.Any())
        {
            TempData["Error"] = $"No enrollees found in {state} state.";
            return RedirectToAction(nameof(Enrollees), new { state });
        }

        // Generate Excel
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("State Enrollees");

        ws.Cells[1, 1].LoadFromCollection(enrollees, true);

        // Styling
        var header = ws.Cells[1, 1, 1, ws.Dimension.End.Column];
        header.Style.Font.Bold = true;
        header.Style.Fill.PatternType = ExcelFillStyle.Solid;
        header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 100, 0));
        header.Style.Font.Color.SetColor(Color.White);

        ws.Cells[ws.Dimension.Address].AutoFitColumns();

        var excelBytes = package.GetAsByteArray();

        return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Enrollees_{state}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReports(
        string? reportingPeriod,
        string? state,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        IQueryable<StateOfficeMonthlyReport> query =
            ApplyMonthlyReportScope(_context.StateOfficeMonthlyReports.AsNoTracking(), user);

        if (!CanManageReports() && User.IsInRole("StateOffice"))
        {
            state = user.State;
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            state = state.Trim();
            query = query.Where(x => x.State == state);
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

        ViewBag.ReportingPeriod = reportingPeriod;
        ViewBag.State = state;
        ViewBag.AvailableStates = await GetReportAvailableStatesAsync(user, cancellationToken);
        ViewBag.CanFilterReportsByState = CanManageReports() || User.IsInRole("HMO");
        ViewBag.CanManageReports = CanManageReports();

        return View(await query
            .OrderByDescending(x => x.ReportingMonth)
            .ThenByDescending(x => x.DateSubmitted)
            .ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> CreateMonthlyReport(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        var model = new StateOfficeMonthlyReportViewModel
        {
            State = CanManageReports() ? string.Empty : await GetDefaultReportStateAsync(user, cancellationToken) ?? string.Empty,
            ReportingOfficerName = user.FullName ?? user.UserName ?? string.Empty
        };

        await PopulateMonthlyReportOptionsAsync(model, user, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMonthlyReport(
        StateOfficeMonthlyReportViewModel model,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        if (!DateTime.TryParseExact(
            $"{model.ReportingPeriod}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime reportingMonth))
        {
            ModelState.AddModelError(nameof(model.ReportingPeriod), "Select a valid reporting month.");
        }

        model.State = model.State?.Trim() ?? string.Empty;
        model.Lga = model.Lga?.Trim() ?? string.Empty;
        model.Ward = model.Ward?.Trim() ?? string.Empty;

        if (!await CanAccessReportStateAsync(model.State, user, cancellationToken))
        {
            return Forbid();
        }

        if (!NorthEastLocationData.IsValidLga(model.State, model.Lga))
        {
            ModelState.AddModelError(nameof(model.Lga), "Select a valid LGA for the chosen state.");
        }

        Provider? facility = null;
        if (model.ProviderId.HasValue)
        {
            facility = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == model.ProviderId.Value && x.State == model.State,
                    cancellationToken);
        }

        if (facility == null
            || !await CanAccessReportProviderAsync(facility.Id, model.State, user, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.ProviderId), "Select a valid facility for the chosen state.");
        }

        StateOfficeMonthlyReportMetricsViewModel? metrics = null;
        if (facility != null
            && ModelState.IsValid)
        {
            metrics = await BuildMonthlyReportMetricsAsync(
                model.State,
                reportingMonth,
                facility.Id,
                model.Lga,
                model.Ward,
                cancellationToken);
        }

        if (!ModelState.IsValid)
        {
            await PopulateMonthlyReportOptionsAsync(model, user, cancellationToken);
            return View(model);
        }

        var report = new StateOfficeMonthlyReport
        {
            ReportingMonth = reportingMonth.Date,
            State = model.State,
            Lga = model.Lga,
            Ward = model.Ward,
            ProviderId = facility!.Id,
            FacilityName = facility.Name,
            FacilityCode = facility.Code,
            ReportingOfficerName = model.ReportingOfficerName.Trim(),
            Designation = model.Designation.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            DateSubmitted = DateTime.UtcNow,
            SubmittedByUserId = user.Id,
            SubmittedByName = user.FullName ?? user.UserName
        };

        ApplyMetrics(report, metrics!);

        _context.StateOfficeMonthlyReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "MonthlyReportSubmitted",
            user.Email ?? User.Identity?.Name ?? "Unknown",
            report.FacilityCode,
            $"{report.State}; {report.FacilityName}; {report.ReportingMonth:yyyy-MM}; Claims:{report.TotalClaims}; Encounters:{report.TotalEncounters}");

        TempData["Success"] = "Monthly reporting information submitted successfully.";
        return RedirectToAction(nameof(MonthlyReportDetails), new { id = report.Id });
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportDetails(int id, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        StateOfficeMonthlyReport? report = await _context.StateOfficeMonthlyReports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (report == null)
        {
            return NotFound();
        }

        if (!await CanAccessMonthlyReportAsync(report, user, cancellationToken))
        {
            return NotFound();
        }

        ViewBag.CanAuditReports = await CanAuditMonthlyReportAsync(report, user, cancellationToken);
        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,StateOffice,HMO")]
    public async Task<IActionResult> AuditMonthlyReport(
        int id,
        string auditStatus,
        string? auditNote,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        StateOfficeMonthlyReport? report = await _context.StateOfficeMonthlyReports
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (report == null)
        {
            return NotFound();
        }

        if (!await CanAuditMonthlyReportAsync(report, user, cancellationToken))
        {
            return Forbid();
        }

        string[] allowedStatuses = { "Audited", "Needs Correction" };
        if (!allowedStatuses.Contains(auditStatus))
        {
            TempData["Error"] = "Select a valid audit decision.";
            return RedirectToAction(nameof(MonthlyReportDetails), new { id });
        }

        report.AuditStatus = auditStatus;
        report.AuditNote = auditNote?.Trim();
        report.AuditedAt = DateTime.UtcNow;
        report.AuditedByUserId = user.Id;
        report.AuditedByName = user.FullName ?? user.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            "MonthlyReportAudited",
            user.Email ?? User.Identity?.Name ?? "Unknown",
            report.FacilityCode,
            $"{report.State}; {report.FacilityName}; {report.ReportingMonth:yyyy-MM}; Status:{report.AuditStatus}; Note:{report.AuditNote}");

        TempData["Success"] = "Monthly report audit updated.";
        return RedirectToAction(nameof(MonthlyReportDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportLgas(string state, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await CanAccessReportStateAsync(state, user, cancellationToken))
        {
            return Forbid();
        }

        var lgas = await GetMonthlyReportLgasAsync(state, cancellationToken);

        return Json(lgas);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportWards(
        string state,
        string lga,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await CanAccessReportStateAsync(state, user, cancellationToken))
        {
            return Forbid();
        }

        var wards = await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.State == state && x.LGA == lga && x.Ward != "")
            .Select(x => x.Ward)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Json(wards);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportFacilities(string state, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await CanAccessReportStateAsync(state, user, cancellationToken))
        {
            return Forbid();
        }

        var facilities = await ApplyProviderReportScope(_context.Providers.AsNoTracking(), user)
            .Where(x => x.State == state)
            .OrderBy(x => x.Name)
            .Select(x => new { id = x.Id, name = x.Name, code = x.Code })
            .ToListAsync(cancellationToken);

        return Json(facilities);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportFacilityDetails(
        string state,
        string reportingPeriod,
        int providerId,
        string? lga,
        string? ward,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null
            || !await CanAccessReportStateAsync(state, user, cancellationToken)
            || !await CanAccessReportProviderAsync(providerId, state, user, cancellationToken))
        {
            return Forbid();
        }

        if (!TryParseReportingMonth(reportingPeriod, out DateTime reportingMonth))
        {
            return BadRequest("Select a valid reporting month.");
        }

        StateOfficeMonthlyReportMetricsViewModel? metrics =
            await BuildMonthlyReportMetricsAsync(
                state.Trim(),
                reportingMonth,
                providerId,
                lga,
                ward,
                cancellationToken);

        return metrics == null
            ? NotFound()
            : Json(metrics);
    }

    [HttpGet]
    public async Task<IActionResult> ExportMonthlyReportDetails(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        StateOfficeMonthlyReport? report = await _context.StateOfficeMonthlyReports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (report == null)
        {
            return NotFound();
        }

        if (!await CanAccessMonthlyReportAsync(report, user, cancellationToken))
        {
            return NotFound();
        }

        StateOfficeMonthlyReportMetricsViewModel metrics = BuildMetricsViewModel(report);
        return BuildMonthlyReportExcel(metrics, $"Monthly_Report_{report.State}_{report.FacilityCode}_{report.ReportingMonth:yyyyMM}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportMonthlyReportSelection(
        string state,
        string reportingPeriod,
        int providerId,
        string? lga,
        string? ward,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null
            || !await CanAccessReportStateAsync(state, user, cancellationToken)
            || !await CanAccessReportProviderAsync(providerId, state, user, cancellationToken))
        {
            return Forbid();
        }

        if (!TryParseReportingMonth(reportingPeriod, out DateTime reportingMonth))
        {
            return BadRequest("Select a valid reporting month.");
        }

        StateOfficeMonthlyReportMetricsViewModel? metrics =
            await BuildMonthlyReportMetricsAsync(
                state.Trim(),
                reportingMonth,
                providerId,
                lga,
                ward,
                cancellationToken);

        if (metrics == null)
        {
            return NotFound();
        }

        return BuildMonthlyReportExcel(metrics, $"Monthly_Report_{metrics.State}_{metrics.FacilityCode}_{reportingMonth:yyyyMM}.xlsx");
    }

    private async Task PopulateMonthlyReportOptionsAsync(
        StateOfficeMonthlyReportViewModel model,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        List<string> states;
        if (CanManageReports())
        {
            states = await GetAvailableStatesAsync(cancellationToken);
        }
        else
        {
            states = await GetReportAvailableStatesAsync(user, cancellationToken);
            if (User.IsInRole("StateOffice"))
            {
                model.State = user.State;
            }
            else if (string.IsNullOrWhiteSpace(model.State) && states.Count == 1)
            {
                model.State = states[0];
            }
        }

        model.States = states
            .Select(x => new SelectListItem(x, x, x == model.State))
            .ToList();

        if (!string.IsNullOrWhiteSpace(model.State))
        {
            model.Lgas = (await GetMonthlyReportLgasAsync(model.State, cancellationToken))
                .Select(x => new SelectListItem(x, x, x == model.Lga))
                .ToList();

            model.Facilities = await ApplyProviderReportScope(_context.Providers.AsNoTracking(), user)
                .Where(x => x.State == model.State)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.ProviderId))
                .ToListAsync(cancellationToken);
        }

        if (model.ProviderId.HasValue)
        {
            model.FacilityCode = await _context.Providers
                .AsNoTracking()
                .Where(x => x.Id == model.ProviderId.Value)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }
    }

    private IQueryable<StateOfficeMonthlyReport> ApplyMonthlyReportScope(
        IQueryable<StateOfficeMonthlyReport> query,
        ApplicationUser user)
    {
        if (CanManageReports())
        {
            return query;
        }

        if (User.IsInRole("HMO") && user.HmoId.HasValue)
        {
            int hmoId = user.HmoId.Value;
            return query.Where(report => _context.Providers
                .Any(provider => provider.Id == report.ProviderId && provider.HmoId == hmoId));
        }

        if (User.IsInRole("StateOffice") && !string.IsNullOrWhiteSpace(user.State))
        {
            string userState = user.State.Trim();
            return query.Where(report => report.State == userState);
        }

        return query.Where(report => false);
    }

    private IQueryable<Provider> ApplyProviderReportScope(
        IQueryable<Provider> query,
        ApplicationUser user)
    {
        if (CanManageReports())
        {
            return query;
        }

        if (User.IsInRole("HMO") && user.HmoId.HasValue)
        {
            int hmoId = user.HmoId.Value;
            return query.Where(provider => provider.HmoId == hmoId);
        }

        if (User.IsInRole("StateOffice") && !string.IsNullOrWhiteSpace(user.State))
        {
            string userState = user.State.Trim();
            return query.Where(provider => provider.State == userState);
        }

        return query.Where(provider => false);
    }

    private async Task<bool> CanAccessReportStateAsync(
        string? state,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        state = state?.Trim();
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        if (CanManageReports())
        {
            return true;
        }

        if (User.IsInRole("StateOffice"))
        {
            return !string.IsNullOrWhiteSpace(user.State)
                && string.Equals(user.State.Trim(), state, StringComparison.OrdinalIgnoreCase);
        }

        if (User.IsInRole("HMO") && user.HmoId.HasValue)
        {
            int hmoId = user.HmoId.Value;
            return await _context.Providers
                .AsNoTracking()
                .AnyAsync(provider => provider.HmoId == hmoId && provider.State == state, cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanAccessReportProviderAsync(
        int providerId,
        string? state,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        state = state?.Trim();
        if (providerId <= 0 || string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        return await ApplyProviderReportScope(_context.Providers.AsNoTracking(), user)
            .AnyAsync(provider => provider.Id == providerId && provider.State == state, cancellationToken);
    }

    private async Task<bool> CanAccessMonthlyReportAsync(
        StateOfficeMonthlyReport report,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (CanManageReports())
        {
            return true;
        }

        if (User.IsInRole("StateOffice"))
        {
            return !string.IsNullOrWhiteSpace(user.State)
                && string.Equals(report.State, user.State.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (User.IsInRole("HMO") && user.HmoId.HasValue)
        {
            int hmoId = user.HmoId.Value;
            return await _context.Providers
                .AsNoTracking()
                .AnyAsync(provider => provider.Id == report.ProviderId && provider.HmoId == hmoId, cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanAuditMonthlyReportAsync(
        StateOfficeMonthlyReport report,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!CanManageReports() && !User.IsInRole("StateOffice") && !User.IsInRole("HMO"))
        {
            return false;
        }

        return await CanAccessMonthlyReportAsync(report, user, cancellationToken);
    }

    private bool CanManageReports()
    {
        return User.IsInRole("CTSHIPAdmin") || User.IsInRole("Admin");
    }

    private static bool TryParseReportingMonth(string? reportingPeriod, out DateTime reportingMonth)
    {
        return DateTime.TryParseExact(
            $"{reportingPeriod}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out reportingMonth);
    }

    private async Task<List<string>> GetMonthlyReportLgasAsync(
        string state,
        CancellationToken cancellationToken)
    {
        List<string> configuredLgas = NorthEastLocationData
            .GetLgas(state)
            .OrderBy(x => x)
            .ToList();

        if (configuredLgas.Count > 0)
        {
            return configuredLgas;
        }

        return await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.State == state && x.LGA != "")
            .Select(x => x.LGA)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private async Task<StateOfficeMonthlyReportMetricsViewModel?> BuildMonthlyReportMetricsAsync(
        string state,
        DateTime reportingMonth,
        int providerId,
        string? lga,
        string? ward,
        CancellationToken cancellationToken)
    {
        DateTime monthStart = new(reportingMonth.Year, reportingMonth.Month, 1);
        DateTime nextMonth = monthStart.AddMonths(1);
        state = state.Trim();

        Provider? facility = await _context.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == providerId && x.State == state,
                cancellationToken);

        if (facility == null)
        {
            return null;
        }

        IQueryable<Enrollee> facilityEnrollees = _context.Enrollees
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.State == state);

        IQueryable<Encounter> monthlyEncounters = _context.Encounters
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId
                && x.VisitDate >= monthStart
                && x.VisitDate < nextMonth);

        IQueryable<Claim> monthlyClaims = _context.Claims
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId
                && x.DateSubmitted >= monthStart
                && x.DateSubmitted < nextMonth);

        string providerIdText = providerId.ToString(CultureInfo.InvariantCulture);
        IQueryable<Referral> monthlyReferrals = _context.Referrals
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.CreatedAt >= monthStart
                && x.CreatedAt < nextMonth
                && ((x.FromProviderId != null
                        && (x.FromProviderId == providerIdText || x.FromProviderId == facility.Code))
                    || x.FromProviderName == facility.Name));

        int totalEncounters = await monthlyEncounters.CountAsync(cancellationToken);
        int serviceUtilization = await _context.EncounterServices
            .AsNoTracking()
            .Where(x => x.Encounter != null
                && x.Encounter.ProviderId == providerId
                && x.Encounter.VisitDate >= monthStart
                && x.Encounter.VisitDate < nextMonth)
            .CountAsync(cancellationToken);

        int totalReferrals = await monthlyReferrals.CountAsync(cancellationToken);
        int completedReferrals = await monthlyReferrals.CountAsync(
            x => x.Status == ReferralStatus.Audited || x.Status == ReferralStatus.Closed,
            cancellationToken);
        int totalClaims = await monthlyClaims.CountAsync(cancellationToken);
        int paidClaims = await monthlyClaims.CountAsync(
            x => x.Status == "Paid",
            cancellationToken);

        return new StateOfficeMonthlyReportMetricsViewModel
        {
            ReportingPeriod = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ReportingMonthDisplay = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            State = state,
            Lga = lga?.Trim() ?? string.Empty,
            Ward = ward?.Trim() ?? string.Empty,
            ProviderId = facility.Id,
            FacilityName = facility.Name,
            FacilityCode = facility.Code,
            TotalActiveEnrollees = await facilityEnrollees.CountAsync(
                x => x.Status == "Active" && x.DateRegistered < nextMonth,
                cancellationToken),
            TotalVisits = await monthlyEncounters
                .Select(x => new { x.EnrolleeId, VisitDay = x.VisitDate.Date })
                .Distinct()
                .CountAsync(cancellationToken),
            TotalEncounters = totalEncounters,
            EnrolleesAccessingCare = await monthlyEncounters
                .Select(x => x.EnrolleeId)
                .Distinct()
                .CountAsync(cancellationToken),
            ServiceUtilization = serviceUtilization,
            TotalReferrals = totalReferrals,
            CompletedReferrals = completedReferrals,
            ReferralCompletionRate = Percentage(completedReferrals, totalReferrals),
            AmountCapitationPaid = 0m,
            CapitationToUtilizationRatio = 0m,
            TotalClaims = totalClaims,
            TotalClaimsAmount = await monthlyClaims
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m,
            PaidClaims = paidClaims,
            PaidClaimsAmount = await monthlyClaims
                .Where(x => x.Status == "Paid")
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m
        };
    }

    private static void ApplyMetrics(
        StateOfficeMonthlyReport report,
        StateOfficeMonthlyReportMetricsViewModel metrics)
    {
        report.TotalActiveEnrollees = metrics.TotalActiveEnrollees;
        report.TotalVisits = metrics.TotalVisits;
        report.TotalEncounters = metrics.TotalEncounters;
        report.EnrolleesAccessingCare = metrics.EnrolleesAccessingCare;
        report.ServiceUtilization = metrics.ServiceUtilization;
        report.TotalReferrals = metrics.TotalReferrals;
        report.CompletedReferrals = metrics.CompletedReferrals;
        report.ReferralCompletionRate = metrics.ReferralCompletionRate;
        report.AmountCapitationPaid = metrics.AmountCapitationPaid;
        report.CapitationToUtilizationRatio = metrics.CapitationToUtilizationRatio;
        report.TotalClaims = metrics.TotalClaims;
        report.TotalClaimsAmount = metrics.TotalClaimsAmount;
        report.PaidClaims = metrics.PaidClaims;
        report.PaidClaimsAmount = metrics.PaidClaimsAmount;
    }

    private static StateOfficeMonthlyReportMetricsViewModel BuildMetricsViewModel(
        StateOfficeMonthlyReport report)
    {
        return new StateOfficeMonthlyReportMetricsViewModel
        {
            ReportingPeriod = report.ReportingMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ReportingMonthDisplay = report.ReportingMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            State = report.State,
            Lga = report.Lga,
            Ward = report.Ward,
            ProviderId = report.ProviderId,
            FacilityName = report.FacilityName,
            FacilityCode = report.FacilityCode,
            TotalActiveEnrollees = report.TotalActiveEnrollees,
            TotalVisits = report.TotalVisits,
            TotalEncounters = report.TotalEncounters,
            EnrolleesAccessingCare = report.EnrolleesAccessingCare,
            ServiceUtilization = report.ServiceUtilization,
            TotalReferrals = report.TotalReferrals,
            CompletedReferrals = report.CompletedReferrals,
            ReferralCompletionRate = report.ReferralCompletionRate,
            AmountCapitationPaid = report.AmountCapitationPaid,
            CapitationToUtilizationRatio = report.CapitationToUtilizationRatio,
            TotalClaims = report.TotalClaims,
            TotalClaimsAmount = report.TotalClaimsAmount,
            PaidClaims = report.PaidClaims,
            PaidClaimsAmount = report.PaidClaimsAmount
        };
    }

    private FileContentResult BuildMonthlyReportExcel(
        StateOfficeMonthlyReportMetricsViewModel metrics,
        string fileName)
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Facility Details");

        ws.Cells[1, 1].Value = "Monthly Report Facility Details";
        ws.Cells[1, 1, 1, 2].Merge = true;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Size = 16;

        object[,] rows =
        {
            { "Reporting Month", metrics.ReportingMonthDisplay },
            { "State", metrics.State },
            { "LGA", metrics.Lga },
            { "Ward", metrics.Ward },
            { "Facility", metrics.FacilityName },
            { "Facility Code", metrics.FacilityCode },
            { "Total Active Enrollees", metrics.TotalActiveEnrollees },
            { "Total Visits", metrics.TotalVisits },
            { "Total Encounters", metrics.TotalEncounters },
            { "Enrollees Accessing Care", metrics.EnrolleesAccessingCare },
            { "Service Utilization", metrics.ServiceUtilization },
            { "Referral Completion", $"{metrics.CompletedReferrals} of {metrics.TotalReferrals} ({metrics.ReferralCompletionRate:N1}%)" },
            { "Amount of Capitation Paid", metrics.AmountCapitationPaid },
            { "Capitation to Utilization Ratio", metrics.CapitationToUtilizationRatio },
            { "Total Claims", metrics.TotalClaims },
            { "Total Claims Amount", metrics.TotalClaimsAmount },
            { "Paid Claims", metrics.PaidClaims },
            { "Paid Claims Amount", metrics.PaidClaimsAmount }
        };

        ws.Cells[3, 1].LoadFromArrays(ToRows(rows));
        ws.Cells[3, 1, 20, 1].Style.Font.Bold = true;
        ws.Cells[3, 1, 20, 2].Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
        ws.Cells[15, 2].Style.Numberformat.Format = "#,##0.00";
        ws.Cells[16, 2].Style.Numberformat.Format = "#,##0.00";
        ws.Cells[18, 2].Style.Numberformat.Format = "#,##0.00";
        ws.Cells[20, 2].Style.Numberformat.Format = "#,##0.00";
        ws.Cells[ws.Dimension.Address].AutoFitColumns();

        byte[] excelBytes = package.GetAsByteArray();
        return File(
            excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static IEnumerable<object[]> ToRows(object[,] rows)
    {
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            yield return new[] { rows[i, 0], rows[i, 1] };
        }
    }

    private static decimal Percentage(int numerator, int denominator)
    {
        return denominator > 0
            ? Math.Round((decimal)numerator / denominator * 100m, 1)
            : 0m;
    }

    private async Task<List<string>> GetAvailableStatesAsync(CancellationToken cancellationToken)
    {
        return await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.State != "")
            .Select(x => x.State)
            .Union(_context.Providers
                .AsNoTracking()
                .Where(x => x.State != "")
                .Select(x => x.State))
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<string>> GetReportAvailableStatesAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (CanManageReports())
        {
            return await GetAvailableStatesAsync(cancellationToken);
        }

        if (User.IsInRole("StateOffice") && !string.IsNullOrWhiteSpace(user.State))
        {
            return new List<string> { user.State.Trim() };
        }

        if (User.IsInRole("HMO") && user.HmoId.HasValue)
        {
            int hmoId = user.HmoId.Value;
            return await _context.Providers
                .AsNoTracking()
                .Where(provider => provider.HmoId == hmoId && provider.State != "")
                .Select(provider => provider.State)
                .Distinct()
                .OrderBy(state => state)
                .ToListAsync(cancellationToken);
        }

        return new List<string>();
    }

    private async Task<string?> GetDefaultReportStateAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        List<string> states = await GetReportAvailableStatesAsync(user, cancellationToken);
        return states.FirstOrDefault();
    }

    private async Task<string?> GetDefaultStateAsync(CancellationToken cancellationToken)
    {
        return await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.State != "")
            .Select(x => x.State)
            .Union(_context.Providers
                .AsNoTracking()
                .Where(x => x.State != "")
                .Select(x => x.State))
            .Distinct()
            .OrderBy(x => x)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
