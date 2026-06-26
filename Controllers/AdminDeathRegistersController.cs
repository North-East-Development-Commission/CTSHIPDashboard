using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/DeathRegisters")]
    public class AdminDeathRegistersController : Controller
    {
        private readonly IDeathRegisterService _deathRegisterService;

        public AdminDeathRegistersController(IDeathRegisterService deathRegisterService)
        {
            _deathRegisterService = deathRegisterService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
        {
            // Use HMO method with null to get non-draft/active records. Admins can filter via search.
            var list = await _deathRegisterService.GetHmoDeathRegistersAsync(null, search, cancellationToken);
            ViewBag.Search = search;
            return View("~/Views/HmoDeathRegisters/Index.cshtml", list);
        }

        [HttpGet("Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var vm = await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);
            if (vm == null) return NotFound();
            return View("~/Views/HmoDeathRegisters/Details.cshtml", vm);
        }

        // Admin can audit
        [HttpGet("Audit/{id:guid}")]
        public async Task<IActionResult> Audit(Guid id, CancellationToken cancellationToken)
        {
            var vm = await _deathRegisterService.BuildAuditViewModelAsync(id, cancellationToken);
            if (vm == null) return NotFound();
            return View("~/Views/HmoDeathRegisters/Audit.cshtml", vm);
        }

        [HttpPost("Audit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Audit(Guid id, Models.ViewModels.DeathRegisterAuditViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid)
            {
                var trustedModel = await _deathRegisterService.BuildAuditViewModelAsync(id, cancellationToken);
                if (trustedModel == null) return NotFound();
                trustedModel.IsApproved = model.IsApproved;
                trustedModel.AuditNote = model.AuditNote;
                return View("~/Views/HmoDeathRegisters/Audit.cshtml", trustedModel);
            }
            string? userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            string? userName = User.Identity?.Name;
            bool ok = await _deathRegisterService.AuditDeathRegisterAsync(model, userId, userName, cancellationToken);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Audit saved." : "Could not audit record.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }
}
