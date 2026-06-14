// Models/AuditLog.cs
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty; // "UserCreated", "RolesUpdated", "UserDeleted"

        [Required]
        public string PerformedBy { get; set; } = string.Empty; // Admin email

        public string? TargetUserEmail { get; set; }

        public string? Details { get; set; } // e.g. "Roles: Admin, SSHIA"

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}