using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class SSHIADashboardViewModel
    {
        public string StateName { get; set; } = string.Empty;
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int NewEnrollmentsThisMonth { get; set; }
        public decimal ActiveEnrolleeRate { get; set; }
        public int TotalProviders { get; set; }
        public int ActiveProviders { get; set; }
        public int PrimaryProviders { get; set; }
        public int TotalHMOs { get; set; }
        public int TotalEncounters { get; set; }
        public int UniqueServiceUsers { get; set; }
        public int EncounterServicesRecorded { get; set; }
        public decimal ServiceUtilizationRate { get; set; }
        public decimal EncounterRatePerThousand { get; set; }
        public ComplaintMetricsViewModel ComplaintMetrics { get; set; } = new();
        public MonitoringDashboardViewModel Monitoring { get; set; } = new();
        public List<ServiceFrequencyViewModel> TopServices { get; set; } = new();
        public List<SSHIAProgramActivityRow> ProgramActivities { get; set; } = new();
        public List<EnrolleeSummaryViewModel> RecentEnrollees { get; set; } = new();
    }

    public class SSHIAProgramActivityRow
    {
        public string Activity { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Rate { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class DrugInventoryIndexViewModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderCode { get; set; } = string.Empty;
        public List<DrugInventoryItem> Items { get; set; } = new();
    }

    public class DrugInventoryFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Drug Title")]
        public string DrugName { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Other Medicine")]
        public string? OtherDrugName { get; set; }

        [StringLength(100)]
        [Display(Name = "Dosage")]
        public string? Strength { get; set; }

        [StringLength(100)]
        [Display(Name = "Dosage Form")]
        public string? DosageForm { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Unit")]
        public string UnitOfMeasure { get; set; } = "Unit";

        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity")]
        public int QuantityOnHand { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        public bool IsActive { get; set; } = true;
        public List<SelectListItem> Units { get; set; } = new();
        public Dictionary<string, List<string>> MedicineGroups { get; set; } = new();
    }

    public class EncounterPrescriptionInputViewModel
    {
        public int? DrugInventoryItemId { get; set; }

        [StringLength(200)]
        public string? DrugName { get; set; }

        [StringLength(200)]
        public string? OtherDrugName { get; set; }

        [StringLength(100)]
        public string? Strength { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Enter a quantity greater than zero.")]
        public int QuantityDispensed { get; set; } = 1;

        [StringLength(20)]
        public string StockStatus { get; set; } = "Instock";
    }
}

