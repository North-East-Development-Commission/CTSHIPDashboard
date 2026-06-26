using CTSHIPDashboard.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class DeathRegisterAuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DeathRegisterId { get; set; }

        public DeathRegister? DeathRegister { get; set; }

        public DeathRegisterAuditAction Action { get; set; }

        [StringLength(450)]
        public string? ActionByUserId { get; set; }

        [StringLength(200)]
        public string? ActionByName { get; set; }

        public DateTime ActionAt { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string? Note { get; set; }
    }

}
