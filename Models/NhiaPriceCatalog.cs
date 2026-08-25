using System.Globalization;

namespace CTSHIPDashboard.Models;

public static class NhiaPriceCatalog
{
    private const string MedicineCatalogFile = "NHIA-Medicine-Price-Catalog-2024.tsv";
    private const string LaboratoryCatalogFile = "NHIA-Professional-FFS-Laboratory-Catalog-2024.tsv";
    private const string SurgeryCatalogFile = "NHIA-Professional-FFS-Surgery-Catalog-2024.tsv";

    public static List<ReferralEncounterClaimCatalogItem> LoadMedicineCatalog(string contentRootPath)
    {
        return LoadCatalog(contentRootPath, MedicineCatalogFile);
    }

    public static List<ReferralEncounterClaimCatalogItem> LoadLaboratoryCatalog(string contentRootPath)
    {
        return LoadCatalog(contentRootPath, LaboratoryCatalogFile);
    }

    public static List<ReferralEncounterClaimCatalogItem> LoadSurgeryCatalog(string contentRootPath)
    {
        return LoadCatalog(contentRootPath, SurgeryCatalogFile);
    }

    private static List<ReferralEncounterClaimCatalogItem> LoadCatalog(string contentRootPath, string fileName)
    {
        string path = Path.Combine(contentRootPath, "App_Data", fileName);
        if (!File.Exists(path))
        {
            return new List<ReferralEncounterClaimCatalogItem>();
        }

        return File.ReadLines(path)
            .Skip(1)
            .Select(line => line.Split('	'))
            .Where(parts => parts.Length >= 3)
            .Select(parts => new
            {
                Title = parts[1].Trim(),
                PriceText = parts[2].Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => decimal.TryParse(
                    item.PriceText,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out decimal price)
                ? new ReferralEncounterClaimCatalogItem(item.Title, price)
                : null)
            .Where(item => item != null)
            .Select(item => item!)
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(item => item.Name)
            .ToList();
    }
}
