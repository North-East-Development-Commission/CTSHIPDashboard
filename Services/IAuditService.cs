using System.Threading.Tasks;

namespace CTSHIPDashboard.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string action,
            string performedBy,
            string? target = null,
            string? details = null,
            CancellationToken cancellationToken = default);
    }
}
