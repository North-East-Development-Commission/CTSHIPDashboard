using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services
{
    public static class ComplaintMetricsService
    {
        public static async Task<ComplaintMetricsViewModel> BuildAsync(
            IQueryable<Complaint> query,
            CancellationToken cancellationToken = default)
        {
            ComplaintMetricsViewModel? metrics = await query
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(group => new ComplaintMetricsViewModel
                {
                    TotalComplaints = group.Count(),
                    OpenComplaints = group.Count(item => item.Status == ComplaintStatus.Open),
                    InProgressComplaints = group.Count(item =>
                        item.Status == ComplaintStatus.InProgress
                        || item.Status == ComplaintStatus.Escalated),
                    ResolvedComplaints = group.Count(item =>
                        item.Status == ComplaintStatus.Resolved
                        || item.Status == ComplaintStatus.Closed),
                    CriticalComplaints = group.Count(item =>
                        item.Priority == ComplaintPriority.Critical
                        && item.Status != ComplaintStatus.Resolved
                        && item.Status != ComplaintStatus.Closed)
                })
                .FirstOrDefaultAsync(cancellationToken);

            metrics ??= new ComplaintMetricsViewModel();
            metrics.ResolutionRate = metrics.TotalComplaints > 0
                ? Math.Round((decimal)metrics.ResolvedComplaints / metrics.TotalComplaints * 100m, 1)
                : 0m;

            return metrics;
        }
    }
}
