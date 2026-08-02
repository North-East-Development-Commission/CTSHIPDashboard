using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    public class DrugInventoryItem
    {
        public int Id { get; set; }

        [Required]
        public int ProviderId { get; set; }
        public Provider? Provider { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Strength { get; set; }

        [StringLength(100)]
        [Display(Name = "Dosage Form")]
        public string? DosageForm { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Unit")]
        public string UnitOfMeasure { get; set; } = "Unit";

        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity On Hand")]
        public int QuantityOnHand { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(200)]
        public string? CreatedByName { get; set; }

        public ICollection<EncounterPrescription> EncounterPrescriptions { get; set; } = new List<EncounterPrescription>();

        [NotMapped]
        public bool IsLowStock => QuantityOnHand <= ReorderLevel;

        [NotMapped]
        public string DisplayName => string.Join(" ", new[] { DrugName, Strength, DosageForm }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}