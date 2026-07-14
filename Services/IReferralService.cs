using CTSHIPDashboard.ViewModels;

namespace CTSHIPDashboard.Services;

public interface IReferralService
{
    Task<List<ReferralIndexViewModel>> GetProviderReferralsAsync(string? providerId, string? search, CancellationToken cancellationToken = default);

    Task<List<ReferralIndexViewModel>> GetHmoReferralsAsync(string? hmoCode, string? search, CancellationToken cancellationToken = default);

    Task<ReferralDetailsViewModel?> GetReferralDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReferralCreateViewModel> BuildCreateViewModelAsync(ReferralCreateViewModel? model = null, CancellationToken cancellationToken = default);

    Task<bool> IsActiveReferralHospitalAsync(Guid? hospitalId, CancellationToken cancellationToken = default);

    Task<Guid> CreateReferralAsync(ReferralCreateViewModel model, string? userId, string? userName, bool submitToHmo, CancellationToken cancellationToken = default);

    Task<bool> SubmitReferralToHmoAsync(Guid referralId, string? userId, string? userName, CancellationToken cancellationToken = default);

    Task<bool> VerifyReferralAsync(ReferralVerificationViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default);

    Task<bool> ReissueReferralVerificationCodeAsync(Guid referralId, string? userId, string? userName, CancellationToken cancellationToken = default);

    Task<ReferralCodeVerificationResult> VerifyReferralCodeAsync(
        ReferralCodeVerificationViewModel model,
        Guid referredHospitalId,
        string? userId,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<bool> AuditReferralAsync(ReferralAuditViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default);

    Task<EncounterReferralInputViewModel> BuildEncounterReferralInputAsync(EncounterReferralInputViewModel? model = null, CancellationToken cancellationToken = default);

    Task<Guid?> CreateReferralFromEncounterAsync(
        Guid encounterId,
        string? encounterReference,
        Guid? enrolleeId,
        string enrolleeNumber,
        string enrolleeFullName,
        string? hmoCode,
        string? hmoName,
        string? fromProviderId,
        string fromProviderName,
        EncounterReferralInputViewModel model,
        string? userId,
        string? userName,
        CancellationToken cancellationToken = default);
}
