using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTHIPDashboard.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Redirect admin users to analytics dashboard for faster access
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("CTSHIPAdmin"))
            {
                return RedirectToAction("Index", "Analytics");
            }
            var model = new HomeViewModel
            {
                News = await _context.NewsUpdates.OrderByDescending(n => n.Date).Take(5).ToListAsync(),
                ProjectOverview = "Community Targeted Social Health Insurance Program (CTSHIP) aims to provide accessible healthcare through PPP model.",
                TotalEnrollees = await _context.Enrollees.CountAsync(),
                AccreditedProviders = await _context.Providers.CountAsync(p => p.IsActive),
                TotalCapitationPaid = (decimal)await _context.Claims.Where(c => c.Status == "Paid").SumAsync(c => c.Amount),
                TotalClaimsProcessed = await _context.Claims.CountAsync(c => c.Status == "Paid"),
                TotalFundsManaged = (decimal)await _context.Claims.SumAsync(c => c.Amount),
                ComplaintMetrics = await ComplaintMetricsService.BuildAsync(_context.Complaints)
            };
            return View(model);
        }

        [Authorize(Roles = "CTSHIPAdmin")]
        public IActionResult AddNews() => View();

        [HttpPost]
        [Authorize(Roles = "CTSHIPAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNews(NewsUpdate news)
        {
            if (ModelState.IsValid)
            {
                _context.NewsUpdates.Add(news);
                await _context.SaveChangesAsync();
                TempData["Success"] = "News added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(news);
        }

        [HttpPost]
        [Authorize(Roles = "CTSHIPAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNews(int id)
        {
            var news = await _context.NewsUpdates.FindAsync(id);
            if (news != null)
            {
                _context.NewsUpdates.Remove(news);
                await _context.SaveChangesAsync();
                TempData["Success"] = "News deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
