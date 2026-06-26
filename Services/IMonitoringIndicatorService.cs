using CTSHIPDashboard.Models.ViewModels;

namespace CTSHIPDashboard.Services
{
    public interface IMonitoringIndicatorService
    {
        Task<MonitoringDashboardViewModel> BuildDashboardAsync(
            string? state,
            CancellationToken cancellationToken = default);
    }
}
