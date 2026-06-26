using System.Security.Claims;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "HMO,NHIA,Admin")]
[Route("Hmos/Referrals")]
public class HmoReferralsController : Controller
{
    private readonly IReferralService _referralService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HmoReferralsController(
        IReferralService referralService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _referralService = referralService;
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
            TempData["ErrorMessage"] = "Your HMO account is not linked to an HMO profile. Please contact the administrator.";
            ViewBag.Search = search;
            return View(new List<ReferralIndexViewModel>());
        }

        List<ReferralIndexViewModel> referrals =
            await _referralService.GetHmoReferralsAsync(hmoCode, search, cancellationToken);

        ViewBag.Search = search;
        return View(referrals);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        return View(referral);
    }

    [HttpGet("Verify/{id:guid}")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken)
    {
        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        ViewBag.Referral = referral;
        return View(new ReferralVerificationViewModel { ReferralId = id, IsApproved = true });
    }

    [HttpPost("Verify/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(
        Guid id,
        ReferralVerificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.ReferralId)
        {
            return BadRequest();
        }

        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Referral = referral;
            return View(model);
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? userName = User.Identity?.Name;
        bool verified =
            await _referralService.VerifyReferralAsync(model, userId, userName, cancellationToken);

        TempData[verified ? "SuccessMessage" : "ErrorMessage"] = verified
            ? "Referral verification completed."
            : "Referral could not be verified. Only referrals submitted to HMO can be verified.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("Audit/{id:guid}")]
    [Authorize(Roles = "NHIA,Admin")]
    public async Task<IActionResult> Audit(Guid id, CancellationToken cancellationToken)
    {
        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        ViewBag.Referral = referral;
        return View(new ReferralAuditViewModel { ReferralId = id });
    }

    [HttpPost("Audit/{id:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "NHIA,Admin")]
    public async Task<IActionResult> Audit(
        Guid id,
        ReferralAuditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.ReferralId)
        {
            return BadRequest();
        }

        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Referral = referral;
            return View(model);
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? userName = User.Identity?.Name;
        bool audited =
            await _referralService.AuditReferralAsync(model, userId, userName, cancellationToken);

        TempData[audited ? "SuccessMessage" : "ErrorMessage"] = audited
            ? "Referral audit completed."
            : "Referral could not be audited. Only verified referrals can be audited.";

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> CanAccessReferralAsync(
        ReferralDetailsViewModel referral,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin") || User.IsInRole("NHIA"))
        {
            return true;
        }

        string? hmoCode = await GetCurrentHmoCodeAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(hmoCode)
            && string.Equals(referral.HmoCode, hmoCode, StringComparison.OrdinalIgnoreCase);
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
