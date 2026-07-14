using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.Models;

public class Referral
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? EncounterId { get; set; }

    [StringLength(100)]
    public string? EncounterReference { get; set; }

    public Guid? EnrolleeId { get; set; }

    [Required]
    [StringLength(100)]
    public string EnrolleeNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string EnrolleeFullName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? HmoCode { get; set; }

    [StringLength(200)]
    public string? HmoName { get; set; }

    [StringLength(100)]
    public string? FromProviderId { get; set; }

    [Required]
    [StringLength(200)]
    public string FromProviderName { get; set; } = string.Empty;

    public Guid ReferredHospitalId { get; set; }

    public ReferredHospital? ReferredHospital { get; set; }

    [Required]
    [StringLength(200)]
    public string Diagnosis { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string ReasonForReferral { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ClinicalSummary { get; set; }

    [StringLength(1000)]
    public string? TreatmentGiven { get; set; }

    [StringLength(1000)]
    public string? InvestigationSummary { get; set; }

    public ReferralPriority Priority { get; set; } = ReferralPriority.Routine;

    public ReferralStatus Status { get; set; } = ReferralStatus.Draft;

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    [StringLength(200)]
    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedToHmoAt { get; set; }

    [StringLength(450)]
    public string? SubmittedByUserId { get; set; }

    [StringLength(450)]
    public string? VerifiedByUserId { get; set; }

    [StringLength(200)]
    public string? VerifiedByName { get; set; }

    public DateTime? VerifiedAt { get; set; }

    [StringLength(1000)]
    public string? HmoVerificationNote { get; set; }

    [StringLength(30)]
    public string? ReferralVerificationCode { get; set; }

    public DateTime? ReferralVerificationCodeIssuedAt { get; set; }

    public DateTime? ReferralVerificationCodeExpiresAt { get; set; }

    [StringLength(450)]
    public string? ReferralVerificationCodeIssuedByUserId { get; set; }

    [StringLength(200)]
    public string? ReferralVerificationCodeIssuedByName { get; set; }

    public DateTime? ReferralVerificationCodeVerifiedAt { get; set; }

    [StringLength(450)]
    public string? ReferralVerificationCodeVerifiedByUserId { get; set; }

    [StringLength(200)]
    public string? ReferralVerificationCodeVerifiedByName { get; set; }

    [StringLength(450)]
    public string? AuditedByUserId { get; set; }

    [StringLength(200)]
    public string? AuditedByName { get; set; }

    public DateTime? AuditedAt { get; set; }

    [StringLength(1000)]
    public string? AuditNote { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<ReferralAuditLog> AuditLogs { get; set; } = new List<ReferralAuditLog>();
}
