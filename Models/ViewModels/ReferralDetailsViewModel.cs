using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.ViewModels;

public class ReferralDetailsViewModel
{
    public Guid Id { get; set; }

    public Guid? EncounterId { get; set; }

    public string? EncounterReference { get; set; }

    public string EnrolleeNumber { get; set; } = string.Empty;

    public string EnrolleeFullName { get; set; } = string.Empty;

    public string? HmoCode { get; set; }

    public string? HmoName { get; set; }

    public string? FromProviderId { get; set; }

    public string FromProviderName { get; set; } = string.Empty;

    public string ReferredHospitalName { get; set; } = string.Empty;

    public string? ReferredHospitalAddress { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public string ReasonForReferral { get; set; } = string.Empty;

    public string? ClinicalSummary { get; set; }

    public string? TreatmentGiven { get; set; }

    public string? InvestigationSummary { get; set; }

    public ReferralPriority Priority { get; set; }

    public ReferralStatus Status { get; set; }

    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SubmittedToHmoAt { get; set; }

    public string? VerifiedByName { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? HmoVerificationNote { get; set; }

    public string? AuditedByName { get; set; }

    public DateTime? AuditedAt { get; set; }

    public string? AuditNote { get; set; }

    public List<ReferralAuditLogViewModel> AuditLogs { get; set; } = new();
}
