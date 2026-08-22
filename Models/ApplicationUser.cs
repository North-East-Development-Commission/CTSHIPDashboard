using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string ContactInfo { get; set; }
        public string FullName { get; set; }
        public string State { get; set; }
        public int? HmoId { get; set; }
        [ForeignKey("HmoId")]
        public virtual Hmo? hmo { get; set; }
        public int? ProviderId { get; set; }
        [ForeignKey("ProviderId")]
        public virtual Provider? Provider { get; set; }
        public int? OrganizationId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        public string? DeletedByName { get; set; }
        public string? DeletionReason { get; set; }
        [ForeignKey("OrganizationId")] // This tells EF which ID to use for this object
        public virtual Organization? Organizations { get; set; }
    }
}
