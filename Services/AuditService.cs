using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using System.Threading.Tasks;

namespace CTSHIPDashboard.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string performedBy, string? target = null, string? details = null)
        {
            var log = new AuditLog
            {
                Action = action,
                PerformedBy = performedBy ?? "Unknown",
                TargetUserEmail = target,
                Details = details,
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
