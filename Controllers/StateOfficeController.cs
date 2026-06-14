using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "StateOffice,Admin,NHIA")]
    public class StateOfficeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StateOfficeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? state)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // If current user is StateOffice, force their state
            if (User.IsInRole("StateOffice"))
            {
                if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.State))
                    return Forbid();
                state = currentUser.State;
            }

            // If no state provided for Admin/NHIA, default to first seeded
            if (string.IsNullOrWhiteSpace(state))
            {
                state = "Borno";
            }

            var vm = new StateOfficeDashboardViewModel
            {
                StateName = state,
                TotalEnrollees = await _context.Enrollees.CountAsync(e => e.State == state),
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

            // Provide states list to Admin/NHIA for quick switch
            if (User.IsInRole("Admin") || User.IsInRole("NHIA"))
            {
                ViewBag.States = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new[] { "Adamawa", "Bauchi", "Borno", "Gombe", "Taraba", "Yobe" }, state);
            }

            return View(vm);
        }
    }
}