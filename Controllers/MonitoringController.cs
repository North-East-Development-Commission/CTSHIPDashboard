using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Admin,NHIA,StateOffice,NEDCAdmin,SSHIA,Monitoring")]
    public class MonitoringController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMonitoringIndicatorService _indicatorService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MonitoringController(
            ApplicationDbContext context,
            IMonitoringIndicatorService indicatorService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _indicatorService = indicatorService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? state, CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (User.IsInRole("StateOffice"))
            {
                if (user == null || string.IsNullOrWhiteSpace(user.State))
                {
                    return Forbid();
                }

                state = user.State;
            }

            MonitoringDashboardViewModel model =
                await _indicatorService.BuildDashboardAsync(state, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,NHIA,StateOffice,Monitoring")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTarget(
            MonitoringTargetViewModel model,
            CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Forbid();
            }

            const string ctsTargetScope = "CTSHIP";
            string scope = string.IsNullOrWhiteSpace(model.Scope)
                ? ctsTargetScope
                : model.Scope.Trim();

            if (string.Equals(scope, "National", StringComparison.OrdinalIgnoreCase))
            {
                scope = ctsTargetScope;
            }

            if (User.IsInRole("StateOffice"))
            {
                if (string.IsNullOrWhiteSpace(user.State)
                    || !string.Equals(scope, user.State, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                scope = user.State;
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Enter a valid target enrolment figure.";
                return RedirectToAction(nameof(Index), new { state = scope == ctsTargetScope ? null : scope });
            }

            ProgramMonitoringTarget? target = await _context.ProgramMonitoringTargets
                .FirstOrDefaultAsync(x => x.Scope == scope, cancellationToken);

            // Preserve an existing programme-wide target while changing its label to CTSHIP.
            if (target == null && scope == ctsTargetScope)
            {
                target = await _context.ProgramMonitoringTargets
                    .FirstOrDefaultAsync(x => x.Scope == "National", cancellationToken);
                if (target != null)
                {
                    target.Scope = ctsTargetScope;
                }
            }

            if (target == null)
            {
                target = new ProgramMonitoringTarget { Scope = scope };
                _context.ProgramMonitoringTargets.Add(target);
            }

            target.TargetEnrollees = model.TargetEnrollees;
            target.UpdatedAt = DateTime.UtcNow;
            target.UpdatedByUserId = user.Id;
            target.UpdatedByName = user.FullName ?? user.UserName;

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"{scope} target enrolment updated.";

            return RedirectToAction(nameof(Index), new { state = scope == ctsTargetScope ? null : scope });
        }
    }
}
