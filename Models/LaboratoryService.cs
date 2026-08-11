using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class LaboratoryService
    {
        public int Id { get; set; }

        [Required]
        public int ProviderId { get; set; }
        public Provider? Provider { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Test / Service Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Service Price")]
        public decimal UnitCost { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(200)]
        public string? CreatedByName { get; set; }
    }
}
