using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider")]
    public class LaboratoryServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LaboratoryServicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null) return RedirectToProviderDashboardWithError();

            return View(new LaboratoryServiceIndexViewModel
            {
                ProviderName = provider.Name,
                ProviderCode = provider.Code,
                Items = await _context.LaboratoryServices
                    .AsNoTracking()
                    .Where(item => item.ProviderId == provider.Id)
                    .OrderBy(item => item.Name)
                    .ToListAsync(cancellationToken)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
        {
            if (await GetPrimaryProviderAsync(cancellationToken) == null) return RedirectToProviderDashboardWithError();
            return View(new LaboratoryServiceFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LaboratoryServiceFormViewModel model, CancellationToken cancellationToken = default)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null) return RedirectToProviderDashboardWithError();

            Normalize(model);
            await ValidateUniqueNameAsync(provider.Id, model.Name, null, cancellationToken);
            if (!ModelState.IsValid) return View(model);

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            _context.LaboratoryServices.Add(new LaboratoryService
            {
                ProviderId = provider.Id,
                Name = model.Name,
                Description = model.Description,
                UnitCost = model.UnitCost,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user?.Id,
                CreatedByName = user?.FullName ?? user?.Email ?? User.Identity?.Name
            });
            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Laboratory service added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            LaboratoryService? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null) return NotFound();

            return View(new LaboratoryServiceFormViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                UnitCost = item.UnitCost,
                IsActive = item.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LaboratoryServiceFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id) return NotFound();
            LaboratoryService? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null) return NotFound();

            Normalize(model);
            await ValidateUniqueNameAsync(item.ProviderId, model.Name, item.Id, cancellationToken);
            if (!ModelState.IsValid) return View(model);

            item.Name = model.Name;
            item.Description = model.Description;
            item.UnitCost = model.UnitCost;
            item.IsActive = model.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Laboratory service updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken = default)
        {
            LaboratoryService? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null) return NotFound();

            item.IsActive = !item.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = item.IsActive ? "Laboratory service activated." : "Laboratory service deactivated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<Provider?> GetPrimaryProviderAsync(CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !user.ProviderId.HasValue) return null;

            return await _context.Providers.AsNoTracking().FirstOrDefaultAsync(provider =>
                provider.Id == user.ProviderId.Value && provider.IsActive && provider.Level == "Primary", cancellationToken);
        }

        private async Task<LaboratoryService?> FindCurrentProviderItemAsync(int id, CancellationToken cancellationToken)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null) return null;
            return await _context.LaboratoryServices.FirstOrDefaultAsync(
                item => item.Id == id && item.ProviderId == provider.Id, cancellationToken);
        }

        private async Task ValidateUniqueNameAsync(
            int providerId,
            string name,
            int? currentId,
            CancellationToken cancellationToken)
        {
            if (await _context.LaboratoryServices.AnyAsync(item =>
                item.ProviderId == providerId && item.Id != currentId && item.Name == name, cancellationToken))
            {
                ModelState.AddModelError(nameof(LaboratoryServiceFormViewModel.Name), "This laboratory service already exists.");
            }
        }

        private static void Normalize(LaboratoryServiceFormViewModel model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        }

        private IActionResult RedirectToProviderDashboardWithError()
        {
            TempData["Error"] = "Laboratory service management is available only to active primary providers.";
            return RedirectToAction("Dashboard", "Providers");
        }
    }
}
