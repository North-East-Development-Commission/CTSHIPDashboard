namespace CTSHIPDashboard.Models;

public sealed record ReferralEncounterClaimCatalogItem(string Name, decimal Price)
{
    public string Label => $"{Name} - ₦{Price:N0}";
}

public static class ReferralEncounterClaimCatalog
{
    public const string PrescriptionService = "Prescription";
    public const string LaboratoryService = "Laboratory";
    public const string SurgeryService = "Surgery";

    public static readonly IReadOnlyList<string> ClaimSupportServices = new[]
    {
        PrescriptionService,
        LaboratoryService,
        SurgeryService
    };

    public static readonly IReadOnlyList<ReferralEncounterClaimCatalogItem> PrescriptionDrugs = new[]
    {
        new ReferralEncounterClaimCatalogItem("Paracetamol 500mg tablets", 1500m),
        new ReferralEncounterClaimCatalogItem("Amoxicillin 500mg capsules", 3500m),
        new ReferralEncounterClaimCatalogItem("Artemether/Lumefantrine ACT", 4500m),
        new ReferralEncounterClaimCatalogItem("Ciprofloxacin 500mg tablets", 3000m),
        new ReferralEncounterClaimCatalogItem("Metronidazole 400mg tablets", 1800m),
        new ReferralEncounterClaimCatalogItem("ORS + Zinc pack", 1200m),
        new ReferralEncounterClaimCatalogItem("Omeprazole 20mg capsules", 2200m),
        new ReferralEncounterClaimCatalogItem("Amlodipine 5mg tablets", 2500m),
        new ReferralEncounterClaimCatalogItem("Metformin 500mg tablets", 2800m),
        new ReferralEncounterClaimCatalogItem("Ceftriaxone injection 1g", 6500m),
        new ReferralEncounterClaimCatalogItem("IV fluids normal saline", 2500m),
        new ReferralEncounterClaimCatalogItem("Diclofenac injection", 1200m)
    };

    public static readonly IReadOnlyList<ReferralEncounterClaimCatalogItem> LaboratoryTests = new[]
    {
        new ReferralEncounterClaimCatalogItem("Full blood count", 3000m),
        new ReferralEncounterClaimCatalogItem("Malaria parasite test", 1500m),
        new ReferralEncounterClaimCatalogItem("Widal test", 2000m),
        new ReferralEncounterClaimCatalogItem("Urinalysis", 1200m),
        new ReferralEncounterClaimCatalogItem("Blood glucose test", 1000m),
        new ReferralEncounterClaimCatalogItem("Pregnancy test", 1200m),
        new ReferralEncounterClaimCatalogItem("HIV screening", 2500m),
        new ReferralEncounterClaimCatalogItem("Hepatitis B surface antigen", 3000m),
        new ReferralEncounterClaimCatalogItem("Urea, electrolytes and creatinine", 6500m),
        new ReferralEncounterClaimCatalogItem("Liver function test", 7000m),
        new ReferralEncounterClaimCatalogItem("Plain X-ray", 8000m),
        new ReferralEncounterClaimCatalogItem("Ultrasound scan", 12000m)
    };

    public static readonly IReadOnlyList<ReferralEncounterClaimCatalogItem> Surgeries = new[]
    {
        new ReferralEncounterClaimCatalogItem("Minor surgical repair / suturing", 15000m),
        new ReferralEncounterClaimCatalogItem("Incision and drainage", 20000m),
        new ReferralEncounterClaimCatalogItem("Wound debridement", 25000m),
        new ReferralEncounterClaimCatalogItem("Fracture immobilization", 60000m),
        new ReferralEncounterClaimCatalogItem("Herniorrhaphy", 100000m),
        new ReferralEncounterClaimCatalogItem("Appendicectomy", 120000m),
        new ReferralEncounterClaimCatalogItem("Cataract surgery", 150000m),
        new ReferralEncounterClaimCatalogItem("Caesarean section", 180000m),
        new ReferralEncounterClaimCatalogItem("Myomectomy", 220000m),
        new ReferralEncounterClaimCatalogItem("Exploratory laparotomy", 250000m)
    };

    public static bool IsClaimSupportService(string service)
    {
        return ClaimSupportServices.Contains(service, StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> NormalizeSelection(
        IEnumerable<string>? selectedItems,
        IReadOnlyList<ReferralEncounterClaimCatalogItem> catalog)
    {
        HashSet<string> validItems = catalog
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (selectedItems ?? Enumerable.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(item => validItems.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static decimal SumSelected(
        IEnumerable<string> selectedItems,
        IReadOnlyList<ReferralEncounterClaimCatalogItem> catalog)
    {
        HashSet<string> selected = selectedItems.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalog
            .Where(item => selected.Contains(item.Name))
            .Sum(item => item.Price);
    }

    public static string DescribeSelected(
        string label,
        IEnumerable<string> selectedItems,
        IReadOnlyList<ReferralEncounterClaimCatalogItem> catalog)
    {
        HashSet<string> selected = selectedItems.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> lines = catalog
            .Where(item => selected.Contains(item.Name))
            .Select(item => $"{item.Name} (₦{item.Price:N0})")
            .ToList();

        return lines.Count == 0
            ? string.Empty
            : $"{label}: {string.Join(", ", lines)}";
    }
}
