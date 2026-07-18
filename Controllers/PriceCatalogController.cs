using System.Globalization;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "CTSHIPAdmin,Admin")]
public class PriceCatalogController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PriceCatalogController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? state,
        string? category,
        CancellationToken cancellationToken = default)
    {
        ReferralPriceCatalogIndexViewModel model =
            await BuildIndexModelAsync(state, category, null, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpload(
        ReferralPriceCatalogIndexViewModel posted,
        CancellationToken cancellationToken = default)
    {
        ReferralPriceCatalogBulkUploadViewModel input = posted.BulkUpload;
        string? normalizedState = NormalizeState(input.State);
        string? normalizedCategory = NormalizeCategory(input.Category);

        if (normalizedState == null)
        {
            ModelState.AddModelError(
                $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{nameof(input.State)}",
                "Select a valid North-East state.");
        }

        if (normalizedCategory == null)
        {
            ModelState.AddModelError(
                $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{nameof(input.Category)}",
                "Select prescription, laboratory, or surgery.");
        }

        List<ParsedCatalogItem> parsedItems = ParseBulkItems(input.ItemsText);
        if (parsedItems.Count == 0)
        {
            ModelState.AddModelError(
                $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{nameof(input.ItemsText)}",
                "Enter at least one catalog item.");
        }

        if (parsedItems.Count > 500)
        {
            ModelState.AddModelError(
                $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{nameof(input.ItemsText)}",
                "Upload 500 or fewer catalog items at a time.");
        }

        if (!ModelState.IsValid || normalizedState == null || normalizedCategory == null)
        {
            ReferralPriceCatalogIndexViewModel model = await BuildIndexModelAsync(
                normalizedState ?? input.State,
                normalizedCategory ?? input.Category,
                input,
                cancellationToken);

            return View(nameof(Index), model);
        }

        input.State = normalizedState;
        input.Category = normalizedCategory;

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        string? actorName = currentUser?.FullName
            ?? currentUser?.Email
            ?? User.Identity?.Name;

        List<ParsedCatalogItem> distinctItems = parsedItems
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        if (input.ReplaceExisting)
        {
            List<ReferralPriceCatalogItem> activeItems = await _context.ReferralPriceCatalogItems
                .Where(item =>
                    item.State == normalizedState &&
                    item.Category == normalizedCategory &&
                    item.IsActive)
                .ToListAsync(cancellationToken);

            foreach (ReferralPriceCatalogItem item in activeItems)
            {
                item.IsActive = false;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        List<string> titles = distinctItems.Select(item => item.Title).ToList();
        List<ReferralPriceCatalogItem> existingItems = await _context.ReferralPriceCatalogItems
            .Where(item =>
                item.State == normalizedState &&
                item.Category == normalizedCategory &&
                titles.Contains(item.Title))
            .ToListAsync(cancellationToken);

        Dictionary<string, ReferralPriceCatalogItem> existingByTitle = existingItems
            .ToDictionary(item => item.Title, StringComparer.OrdinalIgnoreCase);

        int created = 0;
        int updated = 0;

        foreach (ParsedCatalogItem parsedItem in distinctItems)
        {
            if (existingByTitle.TryGetValue(parsedItem.Title, out ReferralPriceCatalogItem? existing))
            {
                existing.Price = parsedItem.Price;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
                continue;
            }

            _context.ReferralPriceCatalogItems.Add(new ReferralPriceCatalogItem
            {
                State = normalizedState,
                Category = normalizedCategory,
                Title = parsedItem.Title,
                Price = parsedItem.Price,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUser?.Id,
                CreatedByName = actorName
            });
            created++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] =
            $"{created} catalog item(s) added and {updated} updated for {normalizedState} {normalizedCategory}.";

        return RedirectToAction(nameof(Index), new { state = normalizedState, category = normalizedCategory });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(
        int id,
        bool isActive,
        string? state,
        string? category,
        CancellationToken cancellationToken = default)
    {
        ReferralPriceCatalogItem? item = await _context.ReferralPriceCatalogItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item == null)
        {
            return NotFound();
        }

        item.IsActive = isActive;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { state, category });
    }

    private async Task<ReferralPriceCatalogIndexViewModel> BuildIndexModelAsync(
        string? state,
        string? category,
        ReferralPriceCatalogBulkUploadViewModel? bulkUpload,
        CancellationToken cancellationToken)
    {
        string? normalizedState = NormalizeState(state);
        string? normalizedCategory = NormalizeCategory(category);

        IQueryable<ReferralPriceCatalogItem> query = _context.ReferralPriceCatalogItems
            .AsNoTracking();

        if (normalizedState != null)
        {
            query = query.Where(item => item.State == normalizedState);
        }

        if (normalizedCategory != null)
        {
            query = query.Where(item => item.Category == normalizedCategory);
        }

        List<ReferralPriceCatalogItemRowViewModel> items = await query
            .OrderBy(item => item.State)
            .ThenBy(item => item.Category)
            .ThenByDescending(item => item.IsActive)
            .ThenBy(item => item.Title)
            .Select(item => new ReferralPriceCatalogItemRowViewModel
            {
                Id = item.Id,
                State = item.State,
                Category = item.Category,
                Title = item.Title,
                Price = item.Price,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        ReferralPriceCatalogBulkUploadViewModel upload = bulkUpload ?? new ReferralPriceCatalogBulkUploadViewModel
        {
            State = normalizedState ?? NorthEastLocationData.States.FirstOrDefault() ?? string.Empty,
            Category = normalizedCategory ?? ReferralEncounterClaimCatalog.PrescriptionService
        };

        upload.State = NormalizeState(upload.State) ?? upload.State;
        upload.Category = NormalizeCategory(upload.Category) ?? upload.Category;

        return new ReferralPriceCatalogIndexViewModel
        {
            State = normalizedState,
            Category = normalizedCategory,
            BulkUpload = upload,
            Items = items,
            StateOptions = BuildStateOptions(upload.State, includeAll: false),
            FilterStateOptions = BuildStateOptions(normalizedState, includeAll: true),
            CategoryOptions = BuildCategoryOptions(upload.Category, includeAll: false),
            FilterCategoryOptions = BuildCategoryOptions(normalizedCategory, includeAll: true)
        };
    }

    private List<ParsedCatalogItem> ParseBulkItems(string? itemsText)
    {
        List<ParsedCatalogItem> items = new();

        if (string.IsNullOrWhiteSpace(itemsText))
        {
            return items;
        }

        string[] lines = itemsText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int separatorIndex = FindSeparatorIndex(line);
            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            {
                AddLineError(index + 1, "Use Title, Price.");
                continue;
            }

            string title = line[..separatorIndex].Trim().Trim('"');
            string priceText = CleanPrice(line[(separatorIndex + 1)..]);

            if (string.IsNullOrWhiteSpace(title))
            {
                AddLineError(index + 1, "Title is required.");
                continue;
            }

            if (title.Length > 200)
            {
                AddLineError(index + 1, "Title must be 200 characters or fewer.");
                continue;
            }

            if (!decimal.TryParse(
                    priceText,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out decimal price)
                || price < 0)
            {
                AddLineError(index + 1, "Price must be a valid non-negative number.");
                continue;
            }

            items.Add(new ParsedCatalogItem(title, price));
        }

        return items;
    }

    private void AddLineError(int lineNumber, string message)
    {
        ModelState.AddModelError(
            $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{nameof(ReferralPriceCatalogBulkUploadViewModel.ItemsText)}",
            $"Line {lineNumber}: {message}");
    }

    private static int FindSeparatorIndex(string line)
    {
        int tabIndex = line.LastIndexOf('\t');
        if (tabIndex >= 0)
        {
            return tabIndex;
        }

        int pipeIndex = line.LastIndexOf('|');
        if (pipeIndex >= 0)
        {
            return pipeIndex;
        }

        return line.IndexOf(',', StringComparison.Ordinal);
    }

    private static string CleanPrice(string value)
    {
        return value
            .Replace("\u20A6", string.Empty, StringComparison.Ordinal)
            .Replace("NGN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string? NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return NorthEastLocationData.States.FirstOrDefault(candidate =>
            string.Equals(candidate, state.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        return ReferralEncounterClaimCatalog.ClaimSupportServices.FirstOrDefault(candidate =>
            string.Equals(candidate, category.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static List<SelectListItem> BuildStateOptions(string? selectedState, bool includeAll)
    {
        List<SelectListItem> options = NorthEastLocationData.States
            .Select(state => new SelectListItem
            {
                Value = state,
                Text = state,
                Selected = string.Equals(state, selectedState, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        if (includeAll)
        {
            options.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "All States",
                Selected = string.IsNullOrWhiteSpace(selectedState)
            });
        }

        return options;
    }

    private static List<SelectListItem> BuildCategoryOptions(string? selectedCategory, bool includeAll)
    {
        List<SelectListItem> options = ReferralEncounterClaimCatalog.ClaimSupportServices
            .Select(category => new SelectListItem
            {
                Value = category,
                Text = category,
                Selected = string.Equals(category, selectedCategory, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        if (includeAll)
        {
            options.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "All Categories",
                Selected = string.IsNullOrWhiteSpace(selectedCategory)
            });
        }

        return options;
    }

    private sealed record ParsedCatalogItem(string Title, decimal Price);
}
