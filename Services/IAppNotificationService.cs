namespace CTSHIPDashboard.Services;

public interface IAppNotificationService
{
    Task NotifyEncounterSubmittedAsync(int encounterId, CancellationToken cancellationToken = default);

    Task NotifyReferralInitiatedAsync(Guid referralId, CancellationToken cancellationToken = default);

    Task NotifyClaimSubmittedAsync(int claimId, Guid? referralId = null, CancellationToken cancellationToken = default);

    Task NotifyComplaintSubmittedAsync(int complaintId, CancellationToken cancellationToken = default);

    Task NotifyMonthlyReportSubmittedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default);

    Task NotifyMonthlyReportAuditedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default);

    Task NotifyMonthlyReportNedcAuditedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default);
}

