// Models/Hmo.cs
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class Hmo
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        public string State { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime DateRegistered { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Active"; // Active, Suspended, Revoked

        public string? LogoPath { get; set; }

        // Navigation
        public virtual ICollection<Enrollee> Enrollees { get; set; } = new List<Enrollee>();
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        public virtual ICollection<Provider> Providers { get; set; } = new List<Provider>();

    }
}