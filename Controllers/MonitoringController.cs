using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin,NHIA,StateOffice,NEDCAdmin,SSHIA,IHSA,Monitoring")]
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
        public async Task<IActionResult> Index(
            string? state,
            string? lga,
            CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
            {
                if (user == null || string.IsNullOrWhiteSpace(user.State))
                {
                    return Forbid();
                }

                state = user.State;
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                lga = null;
            }
            else if (!string.IsNullOrWhiteSpace(lga))
            {
                List<string> availableLgas = await GetAvailableLgasAsync(state, cancellationToken);
                if (!availableLgas.Contains(lga.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    lga = null;
                }
            }

            MonitoringDashboardViewModel model =
                await _indicatorService.BuildDashboardAsync(state, lga, cancellationToken);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Lgas(string state, CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
            {
                if (user == null
                    || string.IsNullOrWhiteSpace(user.State)
                    || !string.Equals(user.State, state, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            return Json(await GetAvailableLgasAsync(state, cancellationToken));
        }

        [HttpPost]
        [Authorize(Roles = "CTSHIPAdmin,NHIA,StateOffice,Monitoring")]
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

            if (User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
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
                return RedirectToAction(nameof(Index), new
                {
                    state = scope == ctsTargetScope ? null : scope,
                    lga = model.Lga
                });
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

            return RedirectToAction(nameof(Index), new
            {
                state = scope == ctsTargetScope ? null : scope,
                lga = model.Lga
            });
        }

        private async Task<List<string>> GetAvailableLgasAsync(
            string? state,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return new List<string>();
            }

            state = state.Trim();
            List<string> configured = NorthEastLocationData.GetLgas(state).ToList();
            List<string> recorded = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State == state && x.LGA != "")
                .Select(x => x.LGA)
                .Distinct()
                .ToListAsync(cancellationToken);

            return configured
                .Concat(recorded)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }
    }
}

