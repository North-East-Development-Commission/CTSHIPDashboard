using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    [Table("Doctors")]
    public class Doctor
    {
        public int Id { get; set; }

        [Required]
        public int ProviderId { get; set; }

        public Provider? Provider { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Hospital Staff Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Staff ID / Licence Number")]
        public string MedicalLicenseNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Display(Name = "Specialty / Unit")]
        public string Specialty { get; set; } = "General Practice";

        [StringLength(150)]
        [Display(Name = "Designation / Rank")]
        public string? Designation { get; set; }

        [Phone]
        [StringLength(30)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? Email { get; set; }

        [Display(Name = "Available for Encounters")]
        public bool IsActive { get; set; } = true;

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();

        [NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(Specialty)
            ? FullName
            : $"{FullName} — {Specialty}";
    }
}
