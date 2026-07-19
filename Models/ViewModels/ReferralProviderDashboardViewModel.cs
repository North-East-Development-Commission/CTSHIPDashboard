using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.ViewModels;

public class ReferralProviderDashboardViewModel
{
    public string FacilityName { get; set; } = string.Empty;

    public string? FacilityState { get; set; }

    public string? FacilityLga { get; set; }

    public bool IsLinkedToReferralHospital { get; set; } = true;

    public int TotalReferrals { get; set; }

    public int ReadyToReceive { get; set; }

    public int Received { get; set; }

    public int Completed { get; set; }

    public int ExpiredCodes { get; set; }

    public int ThisMonth { get; set; }

    public decimal SubmittedClaimValue { get; set; }

    public int TotalClaims { get; set; }

    public int PendingClaims { get; set; }

    public int ApprovedClaims { get; set; }

    public int PaidClaims { get; set; }

    public int RejectedClaims { get; set; }

    public decimal PaidClaimValue { get; set; }

    public int TotalComplaints { get; set; }

    public int OpenComplaints { get; set; }

    public int EscalatedComplaints { get; set; }

    public int ResolvedComplaints { get; set; }

    public decimal CompletionRate =>
        TotalReferrals == 0
            ? 0m
            : Math.Round((decimal)Completed / TotalReferrals * 100m, 1);

    public List<ReferralProviderDashboardAlertViewModel> Alerts { get; set; } = new();

    public List<ReferralProviderDashboardReferralViewModel> RecentReferrals { get; set; } = new();
}

public class ReferralProviderDashboardAlertViewModel
{
    public Guid ReferralId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Icon { get; set; } = "bell";

    public string CssClass { get; set; } = "alert-info";

    public DateTime AlertAt { get; set; }
}

public class ReferralProviderDashboardReferralViewModel
{
    public Guid Id { get; set; }

    public string EnrolleeNumber { get; set; } = string.Empty;

    public string EnrolleeFullName { get; set; } = string.Empty;

    public string FromProviderName { get; set; } = string.Empty;

    public string? HmoName { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public ReferralPriority Priority { get; set; }

    public ReferralStatus Status { get; set; }

    public DateTime ActivityAt { get; set; }

    public DateTime? ReferralVerificationCodeExpiresAt { get; set; }

    public bool ReferralVerificationCodeExpired =>
        ReferralVerificationCodeExpiresAt.HasValue
        && ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow;
}
