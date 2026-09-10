using System.Security.Claims;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "HMO")]
public class HmoUsersController : Controller
{
    private const string OwnerClaim = "CreatedByHmo";
    private static readonly string[] AllowedRoles = { "Provider", "HmoEnrollmentOfficer" };
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public HmoUsersController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    private async Task<int?> HmoIdAsync() => (await _users.GetUserAsync(User))?.HmoId;
    private IQueryable<ApplicationUser> OwnedUsers(int hmoId)
    {
        string owner = hmoId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return _db.Users.Where(u => !u.IsDeleted && u.HmoId == hmoId &&
            _db.UserClaims.Any(c => c.UserId == u.Id && c.ClaimType == OwnerClaim && c.ClaimValue == owner));
    }

    public async Task<IActionResult> Index(string? search, int? providerId)
    {
        int? hmoId = await HmoIdAsync();
        if (!hmoId.HasValue) return Forbid();
        var query = OwnedUsers(hmoId.Value).Include(u => u.Provider).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));
        if (providerId.HasValue) query = query.Where(u => u.ProviderId == providerId);
        ViewBag.Search = search;
        return View(await query.OrderBy(u => u.FullName).ToListAsync());
    }

    public async Task<IActionResult> Edit(string? id)
    {
        int? hmoId = await HmoIdAsync();
        if (!hmoId.HasValue) return Forbid();
        var model = new HmoUserViewModel();
        if (id != null)
        {
            var user = await OwnedUsers(hmoId.Value).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();
            var roles = await _users.GetRolesAsync(user);
            if (roles.Any(r => !AllowedRoles.Contains(r))) return Forbid();
            model = new HmoUserViewModel { Id = id, FullName = user.FullName, Email = user.Email ?? "",
                Phone = user.PhoneNumber, ProviderId = user.ProviderId ?? 0, Roles = roles.ToList(),
                Enabled = !await _users.IsLockedOutAsync(user) };
        }
        await PopulateAsync(model, hmoId.Value);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(HmoUserViewModel model)
    {
        int? hmoId = await HmoIdAsync();
        if (!hmoId.HasValue) return Forbid();
        ApplicationUser? user = null;
        if (model.Id != null)
        {
            user = await OwnedUsers(hmoId.Value).FirstOrDefaultAsync(u => u.Id == model.Id);
            if (user == null) return NotFound();
            if ((await _users.GetRolesAsync(user)).Any(r => !AllowedRoles.Contains(r))) return Forbid();
        }
        model.Roles = (model.Roles ?? new List<string>()).Distinct(StringComparer.Ordinal).ToList();
        if (model.Roles.Count == 0 || model.Roles.Any(r => !AllowedRoles.Contains(r)))
            ModelState.AddModelError(nameof(model.Roles), "Select a facility officer role.");
        var facility = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == model.ProviderId && p.HmoId == hmoId && p.IsActive);
        if (facility == null) ModelState.AddModelError(nameof(model.ProviderId), "Select an active facility under your HMO.");
        if (user == null && string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "An initial password is required.");
        if (ModelState.IsValid)
        {
            IActionResult? outcome = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
            _db.ChangeTracker.Clear();
            user = model.Id == null ? null : await OwnedUsers(hmoId.Value).FirstOrDefaultAsync(u => u.Id == model.Id);
            if (model.Id != null && (user == null || (await _users.GetRolesAsync(user)).Any(r => !AllowedRoles.Contains(r))))
                return (IActionResult)new ForbidResult();
            await using var transaction = await _db.Database.BeginTransactionAsync();
            bool creating = user == null;
            user ??= new ApplicationUser { HmoId = hmoId, EmailConfirmed = true };
            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.UserName = user.Email;
            user.PhoneNumber = model.Phone;
            user.ContactInfo = model.Phone ?? string.Empty;
            user.ProviderId = facility!.Id;
            user.State = facility.State;
            user.LockoutEnabled = true;
            user.LockoutEnd = model.Enabled ? null : DateTimeOffset.MaxValue;
            bool succeeded = AddErrors(creating ? await _users.CreateAsync(user, model.Password!) : await _users.UpdateAsync(user));
            if (succeeded && creating) succeeded = AddErrors(await _users.AddClaimAsync(user,
                new System.Security.Claims.Claim(OwnerClaim, hmoId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            if (succeeded && !creating && !string.IsNullOrWhiteSpace(model.Password))
                succeeded = AddErrors(await _users.ResetPasswordAsync(user, await _users.GeneratePasswordResetTokenAsync(user), model.Password));
            if (succeeded)
            {
                var current = await _users.GetRolesAsync(user);
                succeeded = AddErrors(await _users.RemoveFromRolesAsync(user, current.Except(model.Roles)));
                if (succeeded) succeeded = AddErrors(await _users.AddToRolesAsync(user, model.Roles.Except(current)));
            }
            if (succeeded) succeeded = AddErrors(await _users.UpdateSecurityStampAsync(user));
            if (succeeded)
            {
                await transaction.CommitAsync();
                TempData["Success"] = creating ? "Facility account created." : "Facility account updated.";
                return (IActionResult)RedirectToAction(nameof(Index));
            }
            await transaction.RollbackAsync();
            return null;
            });
            if (outcome != null) return outcome;
        }
        model.Password = null;
        ModelState.SetModelValue(nameof(model.Password), new Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult(string.Empty));
        await PopulateAsync(model, hmoId.Value);
        return View(model);
    }

    private bool AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return result.Succeeded;
    }
    private async Task PopulateAsync(HmoUserViewModel model, int hmoId) => model.Facilities = await _db.Providers
        .AsNoTracking().Where(p => p.HmoId == hmoId && p.IsActive).OrderBy(p => p.Name)
        .Select(p => new SelectListItem(p.Name + " - " + p.State + ", " + p.LGA, p.Id.ToString())).ToListAsync();
}
