using CTSHIPDashboard.Data;
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


[Authorize(Roles = "StateOffice,Admin")]
public class StateOfficeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMonitoringIndicatorService _monitoringIndicatorService;

    public StateOfficeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMonitoringIndicatorService monitoringIndicatorService)
    {
        _context = context;
        _userManager = userManager;
        _monitoringIndicatorService = monitoringIndicatorService;
    }

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
    [Authorize(Roles = "Admin,StateOffice")]
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
        bool isAdmin = User.IsInRole("Admin");
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
    [Authorize(Roles = "Admin,StateOffice")]
    public async Task<IActionResult> Providers(string state = "", string search = "", int page = 1, int pageSize = 20)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(user.State))
            return Forbid();

        if (string.IsNullOrWhiteSpace(state))
            state = user.State;

        // Security: State Officers can only view their own state
        bool isAdmin = User.IsInRole("Admin");
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

    [Authorize(Roles = "Admin,StateOffice")]
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

        if (!User.IsInRole("Admin"))
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
            CapitationDisbursed = await _context.WalletTransactions
                .AsNoTracking()
                .Where(t => t.Type == "Disburse"
                    && t.Amount > 0
                    && t.EnrolleeWallet != null
                    && t.EnrolleeWallet.Enrollee != null
                    && t.EnrolleeWallet.Enrollee.State == state)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m,
            WalletBalance = await _context.EnrolleeWallets
                .AsNoTracking()
                .Where(w => w.Enrollee != null && w.Enrollee.State == state)
                .SumAsync(w => (decimal?)w.Balance, cancellationToken) ?? 0m,
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

    [Authorize(Roles = "Admin,StateOffice")]
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
        if (!User.IsInRole("Admin"))
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
    public async Task<IActionResult> MonthlyReports(string? reportingPeriod, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        IQueryable<StateOfficeMonthlyReport> query = _context.StateOfficeMonthlyReports.AsNoTracking();

        if (!User.IsInRole("Admin"))
        {
            if (string.IsNullOrWhiteSpace(user.State))
            {
                return Forbid();
            }

            query = query.Where(x => x.State == user.State);
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
            State = User.IsInRole("Admin") ? string.Empty : user.State,
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

        if (!User.IsInRole("Admin"))
        {
            if (string.IsNullOrWhiteSpace(user.State)
                || !string.Equals(model.State, user.State, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            model.State = user.State;
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

        Provider? facility = null;
        if (model.ProviderId.HasValue)
        {
            facility = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == model.ProviderId.Value && x.State == model.State,
                    cancellationToken);
        }

        if (facility == null)
        {
            ModelState.AddModelError(nameof(model.ProviderId), "Select a valid facility for the chosen state.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateMonthlyReportOptionsAsync(model, user, cancellationToken);
            return View(model);
        }

        var report = new StateOfficeMonthlyReport
        {
            ReportingMonth = reportingMonth.Date,
            State = model.State.Trim(),
            Lga = model.Lga.Trim(),
            Ward = model.Ward.Trim(),
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

        _context.StateOfficeMonthlyReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

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

        if (!User.IsInRole("Admin")
            && !string.Equals(report.State, user.State, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportLgas(string state, CancellationToken cancellationToken)
    {
        if (!await CanAccessStateAsync(state))
        {
            return Forbid();
        }

        var lgas = await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.State == state && x.LGA != "")
            .Select(x => x.LGA)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Json(lgas);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyReportWards(
        string state,
        string lga,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessStateAsync(state))
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
        if (!await CanAccessStateAsync(state))
        {
            return Forbid();
        }

        var facilities = await _context.Providers
            .AsNoTracking()
            .Where(x => x.State == state)
            .OrderBy(x => x.Name)
            .Select(x => new { id = x.Id, name = x.Name, code = x.Code })
            .ToListAsync(cancellationToken);

        return Json(facilities);
    }

    private async Task PopulateMonthlyReportOptionsAsync(
        StateOfficeMonthlyReportViewModel model,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        List<string> states;
        if (User.IsInRole("Admin"))
        {
            states = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State != "")
                .Select(x => x.State)
                .Union(_context.Providers.Where(x => x.State != "").Select(x => x.State))
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }
        else
        {
            states = string.IsNullOrWhiteSpace(user.State)
                ? new List<string>()
                : new List<string> { user.State };
            model.State = user.State;
        }

        model.States = states
            .Select(x => new SelectListItem(x, x, x == model.State))
            .ToList();

        if (!string.IsNullOrWhiteSpace(model.State))
        {
            model.Lgas = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State == model.State && x.LGA != "")
                .Select(x => x.LGA)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new SelectListItem(x, x, x == model.Lga))
                .ToListAsync(cancellationToken);

            model.Facilities = await _context.Providers
                .AsNoTracking()
                .Where(x => x.State == model.State)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.ProviderId))
                .ToListAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(model.State) && !string.IsNullOrWhiteSpace(model.Lga))
        {
            model.Wards = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State == model.State && x.LGA == model.Lga && x.Ward != "")
                .Select(x => x.Ward)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new SelectListItem(x, x, x == model.Ward))
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

    private async Task<bool> CanAccessStateAsync(string state)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        return user != null
            && !string.IsNullOrWhiteSpace(state)
            && string.Equals(user.State, state, StringComparison.OrdinalIgnoreCase);
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
