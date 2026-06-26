using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    public class NHIAController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NHIAController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var vm = new NHIADashboardViewModel();

            // National Totals
            vm.TotalEnrollees = await _context.Enrollees.CountAsync();
            vm.ActiveEnrollees = await _context.Enrollees.CountAsync(e => e.Status == "Active");
            vm.TotalClaims = await _context.Claims.CountAsync();
            vm.PaidClaims = await _context.Claims.CountAsync(c => c.Status == "Paid");
            vm.TotalHMOs = await _context.Hmos.CountAsync();
            vm.TotalProviders = await _context.Providers.CountAsync();
            vm.TotalClaimAmount = await _context.Claims.SumAsync(c => c.Amount);

            // State-wise Summary (Top 10)
            vm.StateSummaries = await _context.Enrollees
                .GroupBy(e => e.State)
                .Select(g => new StateSummary
                {
                    StateName = g.Key,
                    Enrollees = g.Count(),
                    Claims = _context.Claims.Count(c => c.Enrollee.State == g.Key),
                    Providers = _context.Providers.Count(p => p.State == g.Key),
                    ClaimAmount = _context.Claims.Where(c => c.Enrollee.State == g.Key).Sum(c => c.Amount)
                })
                .OrderByDescending(s => s.Enrollees)
                .Take(10)
                .ToListAsync();

            // Recent Enrollees Nationwide
            vm.RecentEnrollees = await _context.Enrollees
                .Include(e => e.Hmo)
                .OrderByDescending(e => e.DateRegistered)
                .Take(15)
                .Select(e => new RecentEnrollee
                {
                    EnrollmentNumber = e.EnrollmentNumber,
                    FullName = e.FullName,
                    HmoName = e.Hmo != null ? e.Hmo.Name : "N/A",
                    State = e.State,
                    DateRegistered = e.DateRegistered,
                    Status = e.Status ?? "Active"
                })
                .ToListAsync();

            return View(vm);
        }
    }
}
