using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class LaboratoryServiceIndexViewModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderCode { get; set; } = string.Empty;
        public List<LaboratoryService> Items { get; set; } = new();
    }

    public class LaboratoryServiceFormViewModel
    {
        public int Id { get; set; }

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
    }
}
