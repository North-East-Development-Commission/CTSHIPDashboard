using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<AnalyticsHub> _hubContext;
    private readonly ILogger<AppNotificationService> _logger;

    public AppNotificationService(
        ApplicationDbContext context,
        IHubContext<AnalyticsHub> hubContext,
        ILogger<AppNotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyReferralInitiatedAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var referral = await _context.Referrals
                .AsNoTracking()
                .Include(x => x.ReferredHospital)
                .Where(x => x.Id == referralId && !x.IsDeleted)
                .Select(x => new
                {
                    x.Id,
                    x.EnrolleeFullName,
                    x.EnrolleeNumber,
                    x.FromProviderId,
                    x.FromProviderName,
                    x.HmoCode,
                    x.HmoName,
                    x.ReferredHospitalId,
                    ReferredHospitalName = x.ReferredHospital == null
                        ? "referred facility"
                        : x.ReferredHospital.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (referral == null)
            {
                return;
            }

            string? hmoGroup = await ResolveHmoGroupAsync(referral.HmoCode, cancellationToken);

            if (!string.IsNullOrWhiteSpace(hmoGroup))
            {
                await SendNotificationAsync(
                    hmoGroup,
                    "ReferralInitiated",
                    new
                    {
                        Type = "ReferralInitiated",
                        Title = "Referral awaiting HMO action",
                        Message = $"{referral.FromProviderName} initiated a referral for {referral.EnrolleeFullName}. Review and verify it.",
                        Url = $"/Hmos/Referrals/Details/{referral.Id}",
                        Icon = "info",
                        referral.Id,
                        referral.EnrolleeFullName,
                        referral.EnrolleeNumber,
                        referral.FromProviderName,
                        referral.HmoName,
                        referral.ReferredHospitalName
                    },
                    cancellationToken);
            }

            if (TryParseProviderId(referral.FromProviderId, out int providerId))
            {
                await SendNotificationAsync(
                    NotificationGroups.Provider(providerId),
                    "ReferralInitiated",
                    new
                    {
                        Type = "ReferralInitiated",
                        Title = "Referral initiated",
                        Message = $"Referral for {referral.EnrolleeFullName} has been submitted to {referral.HmoName ?? "the HMO"}.",
                        Url = $"/Providers/Referrals/Details/{referral.Id}",
                        Icon = "success",
                        referral.Id,
                        referral.EnrolleeFullName,
                        referral.EnrolleeNumber,
                        referral.FromProviderName,
                        referral.HmoName,
                        referral.ReferredHospitalName
                    },
                    cancellationToken);
            }

            await SendNotificationAsync(
                NotificationGroups.ReferralHospital(referral.ReferredHospitalId),
                "ReferralInitiated",
                new
                {
                    Type = "ReferralInitiated",
                    Title = "Incoming referral",
                    Message = $"{referral.FromProviderName} initiated a referral for {referral.EnrolleeFullName} to your facility.",
                    Url = $"/ReferralPro/Referrals/Details/{referral.Id}",
                    Icon = "info",
                    referral.Id,
                    referral.EnrolleeFullName,
                    referral.EnrolleeNumber,
                    referral.FromProviderName,
                    referral.HmoName,
                    referral.ReferredHospitalName
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send referral notification for referral {ReferralId}.",
                referralId);
        }
    }

    public async Task NotifyClaimSubmittedAsync(
        int claimId,
        Guid? referralId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var claim = await _context.Claims
                .AsNoTracking()
                .Include(x => x.Enrollee)
                .Include(x => x.Hmos)
                .Include(x => x.Provider)
                .Where(x => x.Id == claimId)
                .Select(x => new
                {
                    x.Id,
                    x.ClaimNumber,
                    x.Amount,
                    x.Status,
                    x.HmoId,
                    HmoName = x.Hmos == null ? "the HMO" : x.Hmos.Name,
                    ProviderId = x.ProviderId,
                    ProviderName = x.Provider == null ? "Provider" : x.Provider.Name,
                    EnrolleeName = x.Enrollee == null ? "enrollee" : x.Enrollee.FullName,
                    EnrolleeNumber = x.Enrollee == null ? string.Empty : x.Enrollee.EnrollmentNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (claim == null)
            {
                return;
            }

            if (claim.HmoId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Hmo(claim.HmoId.Value),
                    "ClaimSubmitted",
                    new
                    {
                        Type = "ClaimSubmitted",
                        Title = "Claim awaiting HMO review",
                        Message = $"{claim.ProviderName} submitted claim {claim.ClaimNumber} for {claim.EnrolleeName}.",
                        Url = $"/Claims/Details/{claim.Id}",
                        Icon = "info",
                        claim.Id,
                        claim.ClaimNumber,
                        claim.EnrolleeName,
                        claim.EnrolleeNumber,
                        claim.HmoName,
                        claim.ProviderName,
                        claim.Amount,
                        claim.Status
                    },
                    cancellationToken);
            }

            await SendNotificationAsync(
                NotificationGroups.Provider(claim.ProviderId),
                "ClaimSubmitted",
                new
                {
                    Type = "ClaimSubmitted",
                    Title = "Claim submitted",
                    Message = $"Claim {claim.ClaimNumber} has been submitted to {claim.HmoName}.",
                    Url = $"/Providers/ClaimDetails/{claim.Id}",
                    Icon = "success",
                    claim.Id,
                    claim.ClaimNumber,
                    claim.EnrolleeName,
                    claim.EnrolleeNumber,
                    claim.HmoName,
                    claim.ProviderName,
                    claim.Amount,
                    claim.Status
                },
                cancellationToken);

            if (referralId.HasValue)
            {
                Guid? hospitalId = await _context.Referrals
                    .AsNoTracking()
                    .Where(x => x.Id == referralId.Value && !x.IsDeleted)
                    .Select(x => (Guid?)x.ReferredHospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (hospitalId.HasValue)
                {
                    await SendNotificationAsync(
                        NotificationGroups.ReferralHospital(hospitalId.Value),
                        "ClaimSubmitted",
                        new
                        {
                            Type = "ClaimSubmitted",
                            Title = "Referral claim submitted",
                            Message = $"Claim {claim.ClaimNumber} has been submitted to {claim.HmoName}.",
                            Url = $"/ReferralPro/Referrals/Details/{referralId.Value}",
                            Icon = "success",
                            claim.Id,
                            claim.ClaimNumber,
                            claim.EnrolleeName,
                            claim.EnrolleeNumber,
                            claim.HmoName,
                            claim.ProviderName,
                            claim.Amount,
                            claim.Status,
                            ReferralId = referralId.Value
                        },
                        cancellationToken);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send claim notification for claim {ClaimId}.",
                claimId);
        }
    }

    private async Task<string?> ResolveHmoGroupAsync(
        string? hmoCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hmoCode))
        {
            return null;
        }

        int? hmoId = await _context.Hmos
            .AsNoTracking()
            .Where(x => x.RegistrationNumber == hmoCode.Trim())
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return hmoId.HasValue
            ? NotificationGroups.Hmo(hmoId.Value)
            : NotificationGroups.HmoCode(hmoCode);
    }

    private async Task SendNotificationAsync(
        string group,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group(group).SendAsync(
            "AppNotification",
            payload,
            cancellationToken);

        await _hubContext.Clients.Group(group).SendAsync(
            eventName,
            payload,
            cancellationToken);
    }

    private static bool TryParseProviderId(string? value, out int providerId)
    {
        return int.TryParse(value, out providerId) && providerId > 0;
    }
}
