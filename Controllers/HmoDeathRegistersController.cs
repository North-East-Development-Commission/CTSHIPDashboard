using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "HMO,NHIA,Admin")]
    [Route("Hmos/DeathRegisters")]
    public class HmoDeathRegistersController : Controller
    {
        private readonly IDeathRegisterService _deathRegisterService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HmoDeathRegistersController(
            IDeathRegisterService deathRegisterService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _deathRegisterService = deathRegisterService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
        {
            bool canSeeAll = User.IsInRole("Admin") || User.IsInRole("NHIA");
            string? hmoCode = canSeeAll ? null : await GetCurrentHmoCodeAsync(cancellationToken);

            if (!canSeeAll && string.IsNullOrWhiteSpace(hmoCode))
            {
                TempData["ErrorMessage"] = "Your account is not linked to an HMO. Contact an administrator.";
                return View(new List<DeathRegisterIndexViewModel>());
            }

            List<DeathRegisterIndexViewModel> deathRegisters =
                await _deathRegisterService.GetHmoDeathRegistersAsync(hmoCode, search, cancellationToken);

            ViewBag.Search = search;
            return View(deathRegisters);
        }

        [HttpGet("Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            DeathRegisterDetailsViewModel? deathRegister =
                await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);

            if (deathRegister == null || !await CanAccessDeathRegisterAsync(deathRegister, cancellationToken))
            {
                return NotFound();
            }

            return View(deathRegister);
        }

        [Authorize(Roles = "HMO,Admin")]
        [HttpGet("Verify/{id:guid}")]
        public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken)
        {
            DeathRegisterDetailsViewModel? deathRegister =
                await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);

            if (deathRegister == null || !await CanAccessDeathRegisterAsync(deathRegister, cancellationToken))
            {
                return NotFound();
            }

            DeathRegisterVerificationViewModel? model =
                await _deathRegisterService.BuildVerificationViewModelAsync(id, cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [Authorize(Roles = "HMO,Admin")]
        [HttpPost("Verify/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(
            Guid id,
            DeathRegisterVerificationViewModel model,
            CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            DeathRegisterDetailsViewModel? deathRegister =
                await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);

            if (deathRegister == null || !await CanAccessDeathRegisterAsync(deathRegister, cancellationToken))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                DeathRegisterVerificationViewModel? trustedModel =
                    await _deathRegisterService.BuildVerificationViewModelAsync(id, cancellationToken);
                if (trustedModel == null)
                {
                    return NotFound();
                }

                trustedModel.IsVerified = model.IsVerified;
                trustedModel.HmoVerificationNote = model.HmoVerificationNote;
                return View(trustedModel);
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? userName = User.Identity?.Name;
            bool verified = await _deathRegisterService.VerifyDeathRegisterAsync(
                model,
                userId,
                userName,
                cancellationToken);

            TempData[verified ? "SuccessMessage" : "ErrorMessage"] = verified
                ? "Death register verification saved."
                : "Death register could not be verified. Only records submitted to HMO can be verified.";

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [Authorize(Roles = "NHIA,Admin")]
        [HttpGet("Audit/{id:guid}")]
        public async Task<IActionResult> Audit(Guid id, CancellationToken cancellationToken)
        {
            DeathRegisterAuditViewModel? model =
                await _deathRegisterService.BuildAuditViewModelAsync(id, cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [Authorize(Roles = "NHIA,Admin")]
        [HttpPost("Audit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Audit(
            Guid id,
            DeathRegisterAuditViewModel model,
            CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                DeathRegisterAuditViewModel? trustedModel =
                    await _deathRegisterService.BuildAuditViewModelAsync(id, cancellationToken);
                if (trustedModel == null)
                {
                    return NotFound();
                }

                trustedModel.IsApproved = model.IsApproved;
                trustedModel.AuditNote = model.AuditNote;
                return View(trustedModel);
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? userName = User.Identity?.Name;
            bool audited = await _deathRegisterService.AuditDeathRegisterAsync(
                model,
                userId,
                userName,
                cancellationToken);

            TempData[audited ? "SuccessMessage" : "ErrorMessage"] = audited
                ? "Death register audit saved."
                : "Death register could not be audited. Only HMO verified records can be audited.";

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        private async Task<bool> CanAccessDeathRegisterAsync(
            DeathRegisterDetailsViewModel deathRegister,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole("Admin") || User.IsInRole("NHIA"))
            {
                return true;
            }

            string? hmoCode = await GetCurrentHmoCodeAsync(cancellationToken);
            return !string.IsNullOrWhiteSpace(hmoCode)
                && string.Equals(deathRegister.HmoCode, hmoCode, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string?> GetCurrentHmoCodeAsync(CancellationToken cancellationToken)
        {
            string? claimHmoCode = User.FindFirstValue("HmoCode");
            if (!string.IsNullOrWhiteSpace(claimHmoCode))
            {
                return claimHmoCode.Trim();
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.HmoId == null)
            {
                return null;
            }

            return await _context.Hmos
                .AsNoTracking()
                .Where(x => x.Id == currentUser.HmoId.Value)
                .Select(x => x.RegistrationNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
