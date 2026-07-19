using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace CTSHIPDashboard.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            string performedBy,
            string? target = null,
            string? details = null,
            CancellationToken cancellationToken = default)
        {
            var log = new AuditLog
            {
                Action = string.IsNullOrWhiteSpace(action) ? "System.Activity" : action.Trim(),
                PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? "Unknown" : performedBy.Trim(),
                TargetUserEmail = target,
                Details = details,
                Timestamp = DateTime.Now
            };

            try
            {
                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is DbUpdateException || exception is OperationCanceledException || exception is InvalidOperationException)
            {
                try
                {
                    _context.Entry(log).State = EntityState.Detached;
                }
                catch
                {
                    // The audit entry is best effort; do not let cleanup throw.
                }

                _logger.LogWarning(
                    exception,
                    "Could not write audit log entry for action {AuditAction}.",
                    action);
            }
        }
    }
}
