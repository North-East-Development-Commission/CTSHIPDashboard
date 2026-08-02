using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider")]
    public class DrugInventoryController : Controller
    {
        private static readonly string[] InventoryUnits =
        {
            "Tablet", "Capsule", "Syrup", "Vial", "Ampoule", "Sachet", "Tube", "Bottle", "Pack", "Unit"
        };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DrugInventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null)
            {
                return RedirectToProviderDashboardWithError();
            }

            DrugInventoryIndexViewModel model = new()
            {
                ProviderName = provider.Name,
                ProviderCode = provider.Code,
                Items = await _context.DrugInventoryItems
                    .AsNoTracking()
                    .Where(item => item.ProviderId == provider.Id)
                    .OrderBy(item => item.DrugName)
                    .ThenBy(item => item.Strength)
                    .ToListAsync(cancellationToken)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null)
            {
                return RedirectToProviderDashboardWithError();
            }

            return View(BuildFormModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DrugInventoryFormViewModel model, CancellationToken cancellationToken = default)
        {
            Provider? provider = await GetPrimaryProviderAsync(cancellationToken);
            if (provider == null)
            {
                return RedirectToProviderDashboardWithError();
            }

            NormalizeForm(model);
            await ValidateUniqueInventoryItemAsync(provider.Id, model, null, cancellationToken);

            if (!ModelState.IsValid)
            {
                model.Units = BuildUnitOptions(model.UnitOfMeasure);
                return View(model);
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            DrugInventoryItem item = new()
            {
                ProviderId = provider.Id,
                DrugName = model.DrugName,
                Strength = model.Strength,
                DosageForm = model.DosageForm,
                UnitOfMeasure = model.UnitOfMeasure,
                QuantityOnHand = model.QuantityOnHand,
                ReorderLevel = model.ReorderLevel,
                UnitCost = model.UnitCost,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user?.Id,
                CreatedByName = user?.FullName ?? user?.Email ?? User.Identity?.Name
            };

            _context.DrugInventoryItems.Add(item);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Drug inventory item added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            DrugInventoryItem? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            return View(BuildFormModel(item));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DrugInventoryFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            DrugInventoryItem? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            NormalizeForm(model);
            await ValidateUniqueInventoryItemAsync(item.ProviderId, model, item.Id, cancellationToken);

            if (!ModelState.IsValid)
            {
                model.Units = BuildUnitOptions(model.UnitOfMeasure);
                return View(model);
            }

            item.DrugName = model.DrugName;
            item.Strength = model.Strength;
            item.DosageForm = model.DosageForm;
            item.UnitOfMeasure = model.UnitOfMeasure;
            item.QuantityOnHand = model.QuantityOnHand;
            item.ReorderLevel = model.ReorderLevel;
            item.UnitCost = model.UnitCost;
            item.IsActive = model.IsActive;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Drug inventory item updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(int id, int quantity, CancellationToken cancellationToken = default)
        {
            DrugInventoryItem? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            if (quantity <= 0)
            {
                TempData["Error"] = "Enter a restock quantity greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            item.QuantityOnHand += quantity;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = $"{quantity:N0} {item.UnitOfMeasure.ToLowerInvariant()} added to {item.DrugName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken = default)
        {
            DrugInventoryItem? item = await FindCurrentProviderItemAsync(id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            item.IsActive = !item.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = item.IsActive ? "Inventory item activated." : "Inventory item deactivated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<Provider?> GetPrimaryProviderAsync(CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !user.ProviderId.HasValue)
            {
                return null;
            }

            int providerId = user.ProviderId.GetValueOrDefault();
            return await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(provider =>
                    provider.Id == providerId
                    && provider.IsActive
                    && provider.Level == "Primary",
                    cancellationToken);
        }

        private async Task<DrugInventoryItem?> FindCurrentProviderItemAsync(int id, CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !user.ProviderId.HasValue)
            {
                return null;
            }

            int providerId = user.ProviderId.GetValueOrDefault();
            Provider? provider = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate =>
                    candidate.Id == providerId
                    && candidate.IsActive
                    && candidate.Level == "Primary",
                    cancellationToken);

            if (provider == null)
            {
                return null;
            }

            return await _context.DrugInventoryItems
                .FirstOrDefaultAsync(item => item.Id == id && item.ProviderId == provider.Id, cancellationToken);
        }

        private IActionResult RedirectToProviderDashboardWithError()
        {
            TempData["Error"] = "Drug inventory is available only to active primary providers.";
            return RedirectToAction("Dashboard", "Providers");
        }

        private static DrugInventoryFormViewModel BuildFormModel(DrugInventoryItem? item = null)
        {
            DrugInventoryFormViewModel model = item == null
                ? new DrugInventoryFormViewModel()
                : new DrugInventoryFormViewModel
                {
                    Id = item.Id,
                    DrugName = item.DrugName,
                    Strength = item.Strength,
                    DosageForm = item.DosageForm,
                    UnitOfMeasure = item.UnitOfMeasure,
                    QuantityOnHand = item.QuantityOnHand,
                    ReorderLevel = item.ReorderLevel,
                    UnitCost = item.UnitCost,
                    IsActive = item.IsActive
                };

            model.Units = BuildUnitOptions(model.UnitOfMeasure);
            return model;
        }

        private static List<SelectListItem> BuildUnitOptions(string? selectedUnit = null)
        {
            return InventoryUnits
                .Select(unit => new SelectListItem
                {
                    Value = unit,
                    Text = unit,
                    Selected = string.Equals(unit, selectedUnit, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private static void NormalizeForm(DrugInventoryFormViewModel model)
        {
            model.DrugName = model.DrugName.Trim();
            model.Strength = string.IsNullOrWhiteSpace(model.Strength) ? null : model.Strength.Trim();
            model.DosageForm = string.IsNullOrWhiteSpace(model.DosageForm) ? null : model.DosageForm.Trim();
            model.UnitOfMeasure = string.IsNullOrWhiteSpace(model.UnitOfMeasure) ? "Unit" : model.UnitOfMeasure.Trim();
        }

        private async Task ValidateUniqueInventoryItemAsync(
            int providerId,
            DrugInventoryFormViewModel model,
            int? currentItemId,
            CancellationToken cancellationToken)
        {
            bool duplicate = await _context.DrugInventoryItems.AnyAsync(item =>
                item.ProviderId == providerId
                && item.Id != currentItemId
                && item.DrugName == model.DrugName
                && item.Strength == model.Strength
                && item.DosageForm == model.DosageForm,
                cancellationToken);

            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.DrugName), "This drug, strength, and dosage form already exists in your inventory.");
            }
        }
    }
}