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

[Authorize(Roles = "Provider,Admin")]
[Route("Providers/Referrals")]
public class ProviderReferralsController : Controller
{
    private readonly IReferralService _referralService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProviderReferralsController(
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
        string? providerId = await GetCurrentProviderIdAsync(cancellationToken);
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && string.IsNullOrWhiteSpace(providerId))
        {
            TempData["ErrorMessage"] = "Your account is not linked to a provider facility.";
            ViewBag.Search = search;
            return View(new List<ReferralIndexViewModel>());
        }

        List<ReferralIndexViewModel> referrals =
            await _referralService.GetProviderReferralsAsync(isAdmin ? null : providerId, search, cancellationToken);

        ViewBag.Search = search;
        return View(referrals);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ReferralCreateViewModel model =
            await _referralService.BuildCreateViewModelAsync(null, cancellationToken);

        if (!await ApplyProviderContextAsync(model, cancellationToken))
        {
            TempData["ErrorMessage"] = "Your provider account is not linked to an active facility/HMO.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ReferralCreateViewModel model,
        string submitAction,
        CancellationToken cancellationToken)
    {
        bool hasProviderContext = await ApplyProviderContextAsync(model, cancellationToken);
        ModelState.Remove(nameof(model.FromProviderId));
        ModelState.Remove(nameof(model.FromProviderName));
        ModelState.Remove(nameof(model.HmoCode));
        ModelState.Remove(nameof(model.HmoName));

        if (!hasProviderContext)
        {
            ModelState.AddModelError(string.Empty, "Your provider account is not linked to an active facility/HMO.");
        }

        if (!await _referralService.IsActiveReferralHospitalAsync(model.ReferredHospitalId, cancellationToken))
        {
            ModelState.AddModelError(
                nameof(model.ReferredHospitalId),
                "Select an active referral hospital from the approved list.");
        }

        if (!ModelState.IsValid)
        {
            ReferralCreateViewModel invalidModel =
                await _referralService.BuildCreateViewModelAsync(model, cancellationToken);
            return View(invalidModel);
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? userName = User.Identity?.Name;
        bool submitToHmo = string.Equals(submitAction, "submit", StringComparison.OrdinalIgnoreCase);
        Guid referralId =
            await _referralService.CreateReferralAsync(model, userId, userName, submitToHmo, cancellationToken);

        TempData["SuccessMessage"] = submitToHmo
            ? "Referral created and submitted to HMO for verification."
            : "Referral saved as draft.";

        return RedirectToAction(nameof(Details), new { id = referralId });
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

        if (!await CanAccessProviderReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        return View(referral);
    }

    [HttpPost("Submit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        ReferralDetailsViewModel? referral =
            await _referralService.GetReferralDetailsAsync(id, cancellationToken);

        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessProviderReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? userName = User.Identity?.Name;
        bool submitted =
            await _referralService.SubmitReferralToHmoAsync(id, userId, userName, cancellationToken);

        TempData[submitted ? "SuccessMessage" : "ErrorMessage"] = submitted
            ? "Referral submitted to HMO for verification."
            : "Referral could not be submitted. Only draft referrals can be submitted.";

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> ApplyProviderContextAsync(
        ReferralCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin") && !User.IsInRole("Provider"))
        {
            model.FromProviderId ??= User.FindFirstValue("ProviderId");
            model.FromProviderName = string.IsNullOrWhiteSpace(model.FromProviderName)
                ? User.Identity?.Name ?? "Administrator"
                : model.FromProviderName;
            return true;
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId == null)
        {
            return false;
        }

        Provider? provider = await _context.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == currentUser.ProviderId.Value && x.IsActive,
                cancellationToken);

        if (provider == null)
        {
            return false;
        }

        Hmo? hmo = await _context.Hmos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == provider.HmoId, cancellationToken);

        if (hmo == null)
        {
            return false;
        }

        model.FromProviderId = provider.Id.ToString();
        model.FromProviderName = provider.Name;
        model.HmoCode = hmo.RegistrationNumber;
        model.HmoName = hmo.Name;
        return true;
    }

    private async Task<string?> GetCurrentProviderIdAsync(CancellationToken cancellationToken)
    {
        string? claimProviderId = User.FindFirstValue("ProviderId");
        if (!string.IsNullOrWhiteSpace(claimProviderId))
        {
            return claimProviderId.Trim();
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        return currentUser?.ProviderId?.ToString();
    }

    private async Task<bool> CanAccessProviderReferralAsync(
        ReferralDetailsViewModel referral,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        string? providerId = await GetCurrentProviderIdAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(providerId)
            && string.Equals(referral.FromProviderId, providerId, StringComparison.OrdinalIgnoreCase);
    }
}
