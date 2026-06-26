using CTSHIPDashboard.Models.Enums;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterIndexViewModel
    {
        public Guid Id { get; set; }

        public int? EnrolleeId { get; set; }

        public string EnrolleeNumber { get; set; } = string.Empty;

        public string EnrolleeFullName { get; set; } = string.Empty;

        public string? HmoCode { get; set; }

        public string? HmoName { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        public DateTime DateOfDeath { get; set; }

        public string CauseOfDeath { get; set; } = string.Empty;

        public DeathRegisterStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedToHmoAt { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public DateTime? AuditedAt { get; set; }
    }

}
