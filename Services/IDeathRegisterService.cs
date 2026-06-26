using CTSHIPDashboard.Models.ViewModels;

namespace CTSHIPDashboard.Services
{
    public interface IDeathRegisterService
    {
        Task<List<DeathRegisterIndexViewModel>> GetProviderDeathRegistersAsync(
            string? providerId,
            string? providerCode,
            string? search,
            CancellationToken cancellationToken = default);

        Task<List<DeathRegisterIndexViewModel>> GetHmoDeathRegistersAsync(string? hmoCode, string? search, CancellationToken cancellationToken = default);

        Task<DeathRegisterCreateViewModel> BuildCreateViewModelAsync(DeathRegisterCreateViewModel? model = null, CancellationToken cancellationToken = default);

        Task<Guid> CreateDeathRegisterAsync(DeathRegisterCreateViewModel model, string? userId, string? userName, bool submitToHmo, CancellationToken cancellationToken = default);

        Task<DeathRegisterDetailsViewModel?> GetDeathRegisterDetailsAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> SubmitDeathRegisterToHmoAsync(Guid id, string? userId, string? userName, CancellationToken cancellationToken = default);

        Task<DeathRegisterVerificationViewModel?> BuildVerificationViewModelAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> VerifyDeathRegisterAsync(DeathRegisterVerificationViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default);

        Task<DeathRegisterAuditViewModel?> BuildAuditViewModelAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> AuditDeathRegisterAsync(DeathRegisterAuditViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default);

        Task<Dictionary<int, EnrolleeDeathStatusViewModel>> GetDeathStatusMapAsync(IEnumerable<int> enrolleeIds, CancellationToken cancellationToken = default);

        Task<Dictionary<string, EnrolleeDeathStatusViewModel>> GetDeathStatusMapByEnrolleeNumberAsync(IEnumerable<string> enrolleeNumbers, CancellationToken cancellationToken = default);

        Task<EnrolleeDeathStatusViewModel> GetEnrolleeDeathStatusAsync(int? enrolleeId, string? enrolleeNumber, CancellationToken cancellationToken = default);
    }

}
