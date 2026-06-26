using CTSHIPDashboard.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class DeathRegister
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int? EnrolleeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EnrolleeNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string EnrolleeFullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? HmoCode { get; set; }

        [StringLength(200)]
        public string? HmoName { get; set; }

        [StringLength(100)]
        public string? ProviderId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProviderName { get; set; } = string.Empty;

        [Required]
        public DateTime DateOfDeath { get; set; }

        public TimeSpan? TimeOfDeath { get; set; }

        [Required]
        [StringLength(300)]
        public string PlaceOfDeath { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string CauseOfDeath { get; set; } = string.Empty;

        public DeathCauseCategory CauseCategory { get; set; } = DeathCauseCategory.Unknown;

        [Required]
        [StringLength(200)]
        public string DeathConfirmedBy { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DeathConfirmedByDesignation { get; set; }

        [StringLength(50)]
        public string? DeathConfirmedByPhone { get; set; }

        [StringLength(100)]
        public string? DeathCertificateNumber { get; set; }

        [StringLength(500)]
        public string? DeathCertificateFilePath { get; set; }

        [StringLength(1000)]
        public string? ProviderRemarks { get; set; }

        public DeathRegisterStatus Status { get; set; } = DeathRegisterStatus.Draft;

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(200)]
        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedToHmoAt { get; set; }

        [StringLength(450)]
        public string? SubmittedByUserId { get; set; }

        [StringLength(200)]
        public string? SubmittedByName { get; set; }

        [StringLength(450)]
        public string? VerifiedByUserId { get; set; }

        [StringLength(200)]
        public string? VerifiedByName { get; set; }

        public DateTime? VerifiedAt { get; set; }

        [StringLength(1000)]
        public string? HmoVerificationNote { get; set; }

        [StringLength(450)]
        public string? AuditedByUserId { get; set; }

        [StringLength(200)]
        public string? AuditedByName { get; set; }

        public DateTime? AuditedAt { get; set; }

        [StringLength(1000)]
        public string? AuditNote { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<DeathRegisterAuditLog> AuditLogs { get; set; } = new List<DeathRegisterAuditLog>();
    }

}
