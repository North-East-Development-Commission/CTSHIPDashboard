using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[Authorize(Roles = "Admin,HMO,SSHIA")]
public class AnalyticsController : Controller
{
    private readonly ApplicationDbContext _context;
    public AnalyticsController(ApplicationDbContext context) => _context = context;


    public async Task<IActionResult> Index()
    {
        // Basic KPIs
        ViewBag.TotalEnrollees = await _context.Enrollees.CountAsync();
        ViewBag.ActiveEnrollees = await _context.Enrollees.CountAsync(e => e.Status == "Active");
        ViewBag.ActivePercentage = ViewBag.TotalEnrollees > 0
            ? Math.Round((double)ViewBag.ActiveEnrollees / ViewBag.TotalEnrollees * 100, 1) : 0;

        ViewBag.TotalProviders = await _context.Providers.CountAsync(p => p.IsActive);
        ViewBag.TotalHmos = await _context.Hmos.CountAsync();

        ViewBag.TotalClaims = await _context.Claims.CountAsync();
        ViewBag.PaidClaims = await _context.Claims.CountAsync(c => c.Status == "Paid");
        ViewBag.PendingClaims = await _context.Claims.CountAsync(c => c.Status == "Submitted" || c.Status == "Review Approved");
        ViewBag.TotalPaid = await _context.Claims.Where(c => c.Status == "Paid").SumAsync(c => (decimal?)c.Amount) ?? 0;
        ViewBag.PaidPercentage = ViewBag.TotalClaims > 0
            ? Math.Round((double)ViewBag.PaidClaims / ViewBag.TotalClaims * 100, 1) : 0;

        // ENROLLEES BY STATE — RANKED LIST
        ViewBag.EnrolleesByState = await _context.Enrollees
            .GroupBy(e => e.State ?? "Unknown")
            .Select(g => new
            {
                State = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

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
            .Include(c => c.Enrollee)
            .Where(c => c.Status == "Paid")
            .GroupBy(c => c.Provider)
            .Select(g => new
            {
                g.Key.Name,
                g.Key.State,
                Total = g.Sum(c => c.Amount)
            })
            .OrderByDescending(g => g.Total)
            .Take(5)
            .ToListAsync();

        // Add "Others" only if you limited the results above
        // (Optional - remove if showing all states)

        // Add "Others" if needed
        // var topStates = ViewBag.EnrolleesByState as List<dynamic>;
        // var totalTop = topStates.Sum(s => s.Count);
        // var others = ViewBag.TotalEnrollees - totalTop;
        // if (others > 0)
        // {
        //     topStates.Add(new { State = "Others", Count = others });
        // }

        return View();
    }

    public async Task<IActionResult> Claims()
    {
        var claims = await _context.Claims
            .Include(c => c.Enrollee)
            .Include(c => c.Provider)
            .ToListAsync();

        // Safe calculations — NO MORE EXCEPTION!
        var paidClaims = claims.Where(c => c.Status == "Paid" && c.DatePaid.HasValue).ToList();
        var pendingClaims = claims.Where(c => c.Status == "Submitted" || c.Status == "Approved").ToList();
        var rejectedClaims = claims.Where(c => c.Status == "Rejected").ToList();

        var model = new AnalyticsViewModel
        {
            TotalClaims = claims.Count,
            TotalAmount = (decimal)claims.Sum(c => c.Amount),
            PaidAmount = (decimal)paidClaims.Sum(c => c.Amount),
            PendingAmount = (decimal)pendingClaims.Sum(c => c.Amount),
            RejectedAmount = (decimal)rejectedClaims.Sum(c => c.Amount),

            ApprovalRate = claims.Any()
                ? Math.Round((double)paidClaims.Count / claims.Count * 100, 1)
                : 0,

            // THIS LINE WAS CRASHING — NOW 100% SAFE!
            AverageProcessingDays = paidClaims.Any()
                ? Math.Round(paidClaims.Average(c => (c.DatePaid!.Value - c.DateSubmitted).TotalDays), 1)
                : 0,

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
                    Value = (decimal)g.Sum(c => c.Amount)
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


        var model = new EnrollmentAnalyticsViewModel
        {
            TotalEnrollees = enrollees.Count,
            ActiveEnrollees = enrollees.Count(e => e.Status == "Active"),
            FemaleCount = enrollees.Count(e => e.Gender == "Female"),
            MaleCount = enrollees.Count(e => e.Gender == "Male"),

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
                .Select(g => new ChartData { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList(),

            EnrollmentByHMO = enrollees
                .Where(e => e.Hmo != null)
                .GroupBy(e => e.Hmo!.Email.Split('@')[0].ToUpper())
                .Select(g => new ChartData { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList(),

            RegistrationTrend = enrollees
                .GroupBy(e => new { e.DateRegistered.Year, e.DateRegistered.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new ChartData
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Value = g.Count()
                })
                .Take(12)
                .ToList(),

            // FIXED LINE — NO MORE ERROR!
            CoverageRate = enrollees.Any()
                ? Math.Round((decimal)enrollees.Count / 220_000_000m * 100m, 4)
                : 0m,

            FemalePercentage = enrollees.Any()
                ? Math.Round((decimal)enrollees.Count(e => e.Gender == "Female") / enrollees.Count * 100m, 1)
                : 0m
        };

        return View(model);
    }
}