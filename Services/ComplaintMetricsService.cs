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
            IQueryable<Complaint> scopedQuery = query.AsNoTracking();

            ComplaintMetricsViewModel? metrics = await scopedQuery
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
                        && item.Status != ComplaintStatus.Closed),
                    UnresolvedComplaints = group.Count(item =>
                        item.Status != ComplaintStatus.Resolved
                        && item.Status != ComplaintStatus.Closed
                        && item.Status != ComplaintStatus.Rejected)
                })
                .FirstOrDefaultAsync(cancellationToken);

            metrics ??= new ComplaintMetricsViewModel();
            metrics.ResolutionRate = metrics.TotalComplaints > 0
                ? Math.Round((decimal)metrics.ResolvedComplaints / metrics.TotalComplaints * 100m, 1)
                : 0m;

            int resolvedWithinAgreedTime = await scopedQuery.CountAsync(item =>
                (item.Status == ComplaintStatus.Resolved || item.Status == ComplaintStatus.Closed)
                && item.ResolvedAt.HasValue
                && item.AgreedResolutionDueAt.HasValue
                && item.ResolvedAt <= item.AgreedResolutionDueAt,
                cancellationToken);

            metrics.ResolvedWithinAgreedTimeRate = metrics.ResolvedComplaints > 0
                ? Math.Round((decimal)resolvedWithinAgreedTime / metrics.ResolvedComplaints * 100m, 1)
                : 0m;

            metrics.RecurrentFacilityComplaints = await scopedQuery
                .Where(item => item.ProviderId.HasValue)
                .GroupBy(item => item.ProviderId)
                .Where(group => group.Count() > 1)
                .CountAsync(cancellationToken);

            metrics.RecurrentHmoComplaints = await scopedQuery
                .Where(item => item.HmoId.HasValue)
                .GroupBy(item => item.HmoId)
                .Where(group => group.Count() > 1)
                .CountAsync(cancellationToken);

            return metrics;
        }
    }
}