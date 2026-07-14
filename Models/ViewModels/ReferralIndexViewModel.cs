using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.ViewModels;

public class ReferralIndexViewModel
{
    public Guid Id { get; set; }

    public string EnrolleeNumber { get; set; } = string.Empty;

    public string EnrolleeFullName { get; set; } = string.Empty;

    public string FromProviderName { get; set; } = string.Empty;

    public string ReferredHospitalName { get; set; } = string.Empty;

    public string? HmoName { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public ReferralPriority Priority { get; set; }

    public ReferralStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SubmittedToHmoAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? AuditedAt { get; set; }

    public DateTime? ReferralVerificationCodeExpiresAt { get; set; }

    public DateTime? ReferralVerificationCodeVerifiedAt { get; set; }

    public bool HasActiveReferralVerificationCode =>
        ReferralVerificationCodeExpiresAt.HasValue
        && ReferralVerificationCodeExpiresAt.Value > DateTime.UtcNow;

    public bool ReferralVerificationCodeExpired =>
        ReferralVerificationCodeExpiresAt.HasValue
        && ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow;
}
