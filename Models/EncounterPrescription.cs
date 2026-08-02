using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    public class EncounterPrescription
    {
        public int Id { get; set; }

        [Required]
        public int EncounterId { get; set; }
        public Encounter? Encounter { get; set; }

        [Required]
        public int DrugInventoryItemId { get; set; }
        public DrugInventoryItem? DrugInventoryItem { get; set; }

        [Required]
        [StringLength(200)]
        public string DrugName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Strength { get; set; }

        [StringLength(100)]
        public string? DosageForm { get; set; }

        [Required]
        [StringLength(50)]
        public string UnitOfMeasure { get; set; } = "Unit";

        [Range(1, int.MaxValue)]
        public int QuantityDispensed { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal UnitCost { get; set; }

        public bool InventoryDeducted { get; set; }
        public DateTime DispensedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public decimal TotalCost => UnitCost * QuantityDispensed;
    }
}