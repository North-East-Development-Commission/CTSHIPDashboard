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
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

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

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        using var package = new ExcelPackage();
        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Catalog Prices");

        worksheet.Cells[1, 1].Value = "Title";
        worksheet.Cells[1, 2].Value = "PriceInNaira";
        worksheet.Cells[2, 1].Value = "Paracetamol 500mg tablets";
        worksheet.Cells[2, 2].Value = 1500;
        worksheet.Cells[3, 1].Value = "Full blood count";
        worksheet.Cells[3, 2].Value = 3000;
        worksheet.Column(2).Style.Numberformat.Format = "#,##0.00";

        using (ExcelRange header = worksheet.Cells[1, 1, 1, 2])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(Color.White);
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 112, 59));
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        using (ExcelRange sample = worksheet.Cells[2, 1, 3, 2])
        {
            sample.Style.Fill.PatternType = ExcelFillStyle.Solid;
            sample.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 242, 228));
        }

        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        worksheet.View.FreezePanes(2, 1);
        worksheet.Cells[1, 1, 3, 2].AutoFilter = true;

        ExcelWorksheet guide = package.Workbook.Worksheets.Add("Upload Guide");
        guide.Cells["A1"].Value = "CTSHIP Referral Price Catalog Upload Guide";
        guide.Cells["A1:C1"].Merge = true;
        guide.Cells["A1"].Style.Font.Bold = true;
        guide.Cells["A1"].Style.Font.Size = 16;
        guide.Cells["A1"].Style.Font.Color.SetColor(Color.White);
        guide.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        guide.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 112, 59));
        guide.Cells["A3"].Value = "Column";
        guide.Cells["B3"].Value = "Requirement";
        guide.Cells["C3"].Value = "Example";
        guide.Cells["A4"].Value = "Title";
        guide.Cells["B4"].Value = "Catalog item or service name, 200 characters or fewer.";
        guide.Cells["C4"].Value = "Paracetamol 500mg tablets";
        guide.Cells["A5"].Value = "PriceInNaira";
        guide.Cells["B5"].Value = "Non-negative naira amount. Currency symbols and commas are allowed.";
        guide.Cells["C5"].Value = "1500";
        guide.Cells["A7"].Value = "Select the State and Category on the catalog upload page before importing.";
        guide.Cells["A7:C7"].Merge = true;
        guide.Cells["A8"].Value = "Rows 2 and 3 are examples only. Replace them with real catalog prices.";
        guide.Cells["A8:C8"].Merge = true;

        using (ExcelRange guideHeader = guide.Cells["A3:C3"])
        {
            guideHeader.Style.Font.Bold = true;
            guideHeader.Style.Font.Color.SetColor(Color.White);
            guideHeader.Style.Fill.PatternType = ExcelFillStyle.Solid;
            guideHeader.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(254, 144, 49));
        }

        guide.Cells[guide.Dimension.Address].AutoFitColumns();
        guide.Column(2).Width = Math.Max(guide.Column(2).Width, 60);
        guide.Cells.Style.WrapText = true;

        return File(
            package.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CTSHIP-Referral-Price-Catalog-Template.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpload(
        ReferralPriceCatalogIndexViewModel posted,
        CancellationToken cancellationToken = default)
    {
        ReferralPriceCatalogBulkUploadViewModel input = posted.BulkUpload ?? new ReferralPriceCatalogBulkUploadViewModel();
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

        bool hasExcelFile = input.ExcelFile is { Length: > 0 };
        List<ParsedCatalogItem> parsedItems = new();

        if (hasExcelFile)
        {
            if (!Path.GetExtension(input.ExcelFile!.FileName)
                    .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                AddBulkUploadError(
                    nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile),
                    "Only .xlsx catalog files are allowed.");
            }
            else
            {
                parsedItems = await ParseExcelItemsAsync(input.ExcelFile, cancellationToken);
            }
        }
        else
        {
            parsedItems = ParseBulkItems(input.ItemsText);
        }

        if (parsedItems.Count == 0)
        {
            AddBulkUploadError(
                hasExcelFile
                    ? nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile)
                    : nameof(ReferralPriceCatalogBulkUploadViewModel.ItemsText),
                hasExcelFile
                    ? "The workbook does not contain any valid catalog rows."
                    : "Upload an Excel file or enter at least one catalog item.");
        }

        if (parsedItems.Count > 500)
        {
            AddBulkUploadError(
                hasExcelFile
                    ? nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile)
                    : nameof(ReferralPriceCatalogBulkUploadViewModel.ItemsText),
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(
        ReferralPriceCatalogEditViewModel input,
        CancellationToken cancellationToken = default)
    {
        input.Title = input.Title?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid catalog title and price.";
            return RedirectToAction(nameof(Index), new { state = input.State, category = input.Category });
        }

        ReferralPriceCatalogItem? item = await _context.ReferralPriceCatalogItems
            .FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken);

        if (item == null)
        {
            return NotFound();
        }

        bool duplicateExists = await _context.ReferralPriceCatalogItems
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != input.Id &&
                x.State == item.State &&
                x.Category == item.Category &&
                x.Title == input.Title,
                cancellationToken);

        if (duplicateExists)
        {
            TempData["Error"] =
                $"A catalog item named '{input.Title}' already exists for {item.State} {item.Category}.";
            return RedirectToAction(nameof(Index), new { state = input.State, category = input.Category });
        }

        item.Title = input.Title;
        item.Price = input.Price;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Catalog item updated.";
        return RedirectToAction(nameof(Index), new { state = input.State, category = input.Category });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        int id,
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

        _context.ReferralPriceCatalogItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Catalog item deleted.";
        return RedirectToAction(nameof(Index), new { state, category });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
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

    private async Task<List<ParsedCatalogItem>> ParseExcelItemsAsync(
        IFormFile excelFile,
        CancellationToken cancellationToken)
    {
        List<ParsedCatalogItem> items = new();

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            ExcelWorksheet? worksheet = package.Workbook.Worksheets
                .FirstOrDefault(sheet => sheet.Dimension != null);

            if (worksheet?.Dimension == null)
            {
                AddBulkUploadError(
                    nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile),
                    "The workbook does not contain any catalog data.");
                return items;
            }

            Dictionary<string, int> headerMap = new(StringComparer.OrdinalIgnoreCase);
            for (int column = 1; column <= worksheet.Dimension.End.Column; column++)
            {
                string normalized = NormalizeExcelHeader(worksheet.Cells[1, column].Text);
                if (!string.IsNullOrWhiteSpace(normalized) && !headerMap.ContainsKey(normalized))
                {
                    headerMap[normalized] = column;
                }
            }

            int? titleColumn = FindExcelColumn(
                headerMap,
                "Title",
                "Catalog Title",
                "CatalogTitle",
                "Item",
                "Service",
                "Service Title");
            int? priceColumn = FindExcelColumn(
                headerMap,
                "PriceInNaira",
                "Price In Naira",
                "Price",
                "Amount",
                "Cost",
                "Naira");

            if (!titleColumn.HasValue || !priceColumn.HasValue)
            {
                AddBulkUploadError(
                    nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile),
                    "The workbook must include Title and PriceInNaira columns.");
                return items;
            }

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string title = worksheet.Cells[row, titleColumn.Value].Text.Trim();
                string priceText = worksheet.Cells[row, priceColumn.Value].Text.Trim();

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(priceText))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    AddExcelRowError(row, "Title is required.");
                    continue;
                }

                if (title.Length > 200)
                {
                    AddExcelRowError(row, "Title must be 200 characters or fewer.");
                    continue;
                }

                if (!TryReadPrice(worksheet.Cells[row, priceColumn.Value], out decimal price))
                {
                    AddExcelRowError(row, "PriceInNaira must be a valid non-negative naira amount.");
                    continue;
                }

                items.Add(new ParsedCatalogItem(title.Trim('"'), price));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            AddBulkUploadError(
                nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile),
                "The Excel file could not be read. Confirm that it uses the downloaded .xlsx template.");
        }

        return items;
    }

    private void AddLineError(int lineNumber, string message)
    {
        AddBulkUploadError(
            nameof(ReferralPriceCatalogBulkUploadViewModel.ItemsText),
            $"Line {lineNumber}: {message}");
    }

    private void AddExcelRowError(int rowNumber, string message)
    {
        AddBulkUploadError(
            nameof(ReferralPriceCatalogBulkUploadViewModel.ExcelFile),
            $"Row {rowNumber}: {message}");
    }

    private void AddBulkUploadError(string fieldName, string message)
    {
        ModelState.AddModelError(
            $"{nameof(ReferralPriceCatalogIndexViewModel.BulkUpload)}.{fieldName}",
            message);
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
            .Replace("NAIRA", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool TryReadPrice(ExcelRange cell, out decimal price)
    {
        price = 0m;

        if (cell.Value is decimal decimalValue)
        {
            price = decimalValue;
            return price >= 0m;
        }

        if (cell.Value is double doubleValue)
        {
            price = Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
            return price >= 0m;
        }

        if (cell.Value is int intValue)
        {
            price = intValue;
            return price >= 0m;
        }

        string priceText = CleanPrice(cell.Text);
        return decimal.TryParse(
                priceText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out price)
            && price >= 0m;
    }

    private static int? FindExcelColumn(
        Dictionary<string, int> headerMap,
        params string[] headerCandidates)
    {
        foreach (string header in headerCandidates)
        {
            if (headerMap.TryGetValue(NormalizeExcelHeader(header), out int column))
            {
                return column;
            }
        }

        return null;
    }

    private static string NormalizeExcelHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
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
