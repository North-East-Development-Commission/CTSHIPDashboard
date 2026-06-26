using CTSHIPDashboard.Models.Enums;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterDetailsViewModel
    {
        public Guid Id { get; set; }

        public int? EnrolleeId { get; set; }

        public string EnrolleeNumber { get; set; } = string.Empty;

        public string EnrolleeFullName { get; set; } = string.Empty;

        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }

        public string? HmoCode { get; set; }

        public string? HmoName { get; set; }

        public string? ProviderId { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        public DateTime DateOfDeath { get; set; }

        public TimeSpan? TimeOfDeath { get; set; }

        public string PlaceOfDeath { get; set; } = string.Empty;

        public string CauseOfDeath { get; set; } = string.Empty;

        public DeathCauseCategory CauseCategory { get; set; }

        public string DeathConfirmedBy { get; set; } = string.Empty;

        public string? DeathConfirmedByDesignation { get; set; }

        public string? DeathConfirmedByPhone { get; set; }

        public string? DeathCertificateNumber { get; set; }

        public string? DeathCertificateFilePath { get; set; }

        public string? ProviderRemarks { get; set; }

        public DeathRegisterStatus Status { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? SubmittedByName { get; set; }

        public DateTime? SubmittedToHmoAt { get; set; }

        public string? VerifiedByName { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public string? HmoVerificationNote { get; set; }

        public string? AuditedByName { get; set; }

        public DateTime? AuditedAt { get; set; }

        public string? AuditNote { get; set; }

        public List<DeathRegisterAuditLogViewModel> AuditLogs { get; set; } = new();
    }

}
