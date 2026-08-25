using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using CTSHIPDashboard.Services;

[Authorize(Roles = "CTSHIPAdmin,Admin, NEDCAdmin")]
public class AnalyticsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMonitoringIndicatorService _monitoringIndicatorService;

    public AnalyticsController(
        ApplicationDbContext context,
        IMonitoringIndicatorService monitoringIndicatorService)
    {
        _context = context;
        _monitoringIndicatorService = monitoringIndicatorService;
    }


    public async Task<IActionResult> Index()
    {
        MonitoringDashboardViewModel monitoring =
            await _monitoringIndicatorService.BuildDashboardAsync(null);

        // TOP 5 HMOS
        ViewBag.TopHmos = await _context.Hmos
            .Include(h => h.Enrollees)
            .OrderByDescending(h => h.Enrollees.Count)
            .Take(5)
            .Select(h => new { h.Name, Count = h.Enrollees.Count })
            .ToListAsync();

        // TOP 5 PROVIDERS BY PAID CLAIMS VALUE
        ViewBag.TopProviders = await _context.Claims
            .Include(c => c.Provider)
            .Where(c => c.Status == "Paid" && c.Provider != null)
            .GroupBy(c => new
            {
                c.ProviderId,
                ProviderName = c.Provider!.Name,
                ProviderState = c.Provider.State
            })
            .Select(g => new
            {
                Name = g.Key.ProviderName,
                State = g.Key.ProviderState,
                Total = g.Sum(c => c.Amount)
            })
            .OrderByDescending(g => g.Total)
            .Take(5)
            .ToListAsync();

        return View(monitoring);
    }
    public async Task<IActionResult> Claims()
    {
        var claims = await _context.Claims
            .Include(c => c.Enrollee)
            .Include(c => c.Provider)
            .Include(c => c.Queries)
            .ToListAsync();

        ClaimMatrixViewModel claimMatrix = ClaimMetricsService.Build(claims);
        var rejectedClaims = claims.Where(ClaimMetricsService.IsRejected).ToList();

        var model = new AnalyticsViewModel
        {
            TotalClaims = claimMatrix.TotalClaims,
            SubmittedClaims = claimMatrix.SubmittedClaims,
            ClaimsValidated = claimMatrix.ClaimsValidated,
            QueryClaims = claimMatrix.QueryClaims,
            PaidClaims = claimMatrix.PaidClaims,
            RejectedClaims = claimMatrix.RejectedClaims,
            OutstandingClaims = claimMatrix.OutstandingClaims,
            TotalAmount = claimMatrix.TotalClaimAmount,
            ApprovedAmount = claimMatrix.ApprovedClaimAmount,
            PaidAmount = claimMatrix.PaidClaimAmount,
            PendingAmount = claimMatrix.TotalClaimAmount,
            RejectedAmount = rejectedClaims.Sum(c => c.Amount),
            OutstandingAmount = claimMatrix.OutstandingClaimAmount,
            ApprovalRate = claimMatrix.TotalClaims > 0
                ? Math.Round((double)claimMatrix.PaidClaims / claimMatrix.TotalClaims * 100, 1)
                : 0,
            AverageProcessingDays = claimMatrix.AverageProcessingDays,
            ClaimsByState = claims
                .GroupBy(c => c.Enrollee?.State ?? "Unknown")
                .Select(g => new ChartData { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList(),
            ClaimsByMonth = claims
                .GroupBy(c => new { c.DateSubmitted.Year, c.DateSubmitted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new ChartData
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Value = g.Sum(c => c.Amount)
                })
                .Take(12)
                .ToList(),
            TopDiagnoses = claims
                .Where(c => !string.IsNullOrEmpty(c.Diagnosis))
                .GroupBy(c => c.Diagnosis.Trim().Split(' ')[0].ToLower())
                .Select(g => new ChartData
                {
                    Label = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(g.Key),
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList()
        };

        return View(model);
    }
    public async Task<IActionResult> Enrollment()
    {
        var enrollees = await _context.Enrollees
            .Include(e => e.Hmo)
            .ToListAsync();

        var total = enrollees.Count;

        var model = new EnrollmentAnalyticsViewModel
        {
            TotalEnrollees = total,
            ActiveEnrollees = enrollees.Count(e => e.Status == "Active"),
            FemaleCount = enrollees.Count(e => e.Gender == "Female"),
            MaleCount = enrollees.Count(e => e.Gender == "Male"),

            // Explicitly compute percentages expected by your View layout
            FemalePercentage = total > 0 ? (int)Math.Round((double)enrollees.Count(e => e.Gender == "Female") / total * 100) : 0,
            CoverageRate = (decimal)(total > 0 ? Math.Round((double)total / 220000000 * 100, 4) : 0), // Out of 220M population

            AgeGroups = new Dictionary<string, int>
            {
                ["0-4 years"] = enrollees.Count(e => e.DateOfBirth >= DateTime.Now.AddYears(-5)),
                ["5-14 years"] = enrollees.Count(e => e.DateOfBirth >= DateTime.Now.AddYears(-15) && e.DateOfBirth < DateTime.Now.AddYears(-5)),
                ["15-24 years"] = enrollees.Count(e => e.DateOfBirth >= DateTime.Now.AddYears(-25) && e.DateOfBirth < DateTime.Now.AddYears(-15)),
                ["25-54 years"] = enrollees.Count(e => e.DateOfBirth >= DateTime.Now.AddYears(-55) && e.DateOfBirth < DateTime.Now.AddYears(-25)),
                ["55+ years"] = enrollees.Count(e => e.DateOfBirth < DateTime.Now.AddYears(-55))
            },

            EnrollmentByState = enrollees
                .GroupBy(e => e.State ?? "Unknown")
                .Select(g => new ChartData { Label = g.Key, Value = (decimal)g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList(),

            EnrollmentByHMO = enrollees
                .Where(e => e.Hmo != null)
                .GroupBy(e => e.Hmo.Name ?? "Unknown HMO") // Clean Grouping using Name
                .Select(g => new ChartData { Label = g.Key, Value = (decimal)g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList(),

            RegistrationTrend = enrollees
                .GroupBy(e => new { e.DateRegistered.Year, e.DateRegistered.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new ChartData
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Value = (decimal)g.Count()
                })
                .Take(12)
                .ToList()
        };

        return View(model);
    }
}
