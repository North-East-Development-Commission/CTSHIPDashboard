using CTSHIPDashboard.Models.Enums;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterAuditLogViewModel
    {
        public DeathRegisterAuditAction Action { get; set; }

        public string? ActionByName { get; set; }

        public DateTime ActionAt { get; set; }

        public string? Note { get; set; }
    }

}
