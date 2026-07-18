namespace CTSHIPDashboard.Services;

public interface IAppNotificationService
{
    Task NotifyReferralInitiatedAsync(Guid referralId, CancellationToken cancellationToken = default);

    Task NotifyClaimSubmittedAsync(int claimId, Guid? referralId = null, CancellationToken cancellationToken = default);
}
