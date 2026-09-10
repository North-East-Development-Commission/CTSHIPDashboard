using CTSHIPDashboard.Services;

sealed class TestNotifications : IAuditService, IAppNotificationService
{
    public Task LogAsync(string action, string performedBy, string? target = null, string? details = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyEncounterSubmittedAsync(int encounterId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyReferralInitiatedAsync(Guid referralId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyClaimSubmittedAsync(int claimId, Guid? referralId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyComplaintSubmittedAsync(int complaintId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyMonthlyReportSubmittedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyMonthlyReportAuditedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyMonthlyReportNedcAuditedAsync(int reportId, bool isReferralProviderReport = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
