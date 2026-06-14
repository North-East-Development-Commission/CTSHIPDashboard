

using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Pages.Nhia
{
    [Authorize(Roles = "NHIA,Admin")]
    public class Index
    {
        private readonly ApplicationDbContext _context;

        public Index(ApplicationDbContext context) => _context = context;

        public NHIAKpiViewModel Kpis { get; set; } = new NHIAKpiViewModel();

        public async Task OnGetAsync()
        {
            Kpis.TotalEnrollees = await _context.Enrollees.CountAsync();
            Kpis.ActiveEnrollees = await _context.Enrollees.CountAsync(e => e.Status == "Active");
            Kpis.TotalProviders = await _context.Providers.CountAsync(p => p.IsActive);
            Kpis.TotalHmos = await _context.Hmos.CountAsync();

            Kpis.TotalClaims = await _context.Claims.CountAsync();
            Kpis.PaidClaims = await _context.Claims.CountAsync(c => c.Status == "Paid");
            Kpis.PendingClaims = await _context.Claims.CountAsync(c => c.Status == "Submitted" || c.Status == "Pending" || c.Status == "Approved");
            Kpis.TotalPaidAmount = await _context.Claims.Where(c => c.Status == "Paid").SumAsync(c => (decimal?)c.Amount) ?? 0m;

            // Enrollees by state (top 10)
            Kpis.EnrolleesByState = await _context.Enrollees
                .GroupBy(e => e.State ?? "Unknown")
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(k => k.Value)
                .Take(10)
                .ToListAsync();

            // Top HMOs by enrollee count
            Kpis.TopHmos = await _context.Hmos
                .Include(h => h.Enrollees)
                .Select(h => new KeyValuePair<string, int>(h.Name, h.Enrollees.Count))
                .OrderByDescending(k => k.Value)
                .Take(8)
                .ToListAsync();

            // Claims by state
            Kpis.ClaimsByState = await _context.Claims
                .Include(c => c.Enrollee)
                .Where(c => c.Enrollee != null)
                .GroupBy(c => c.Enrollee!.State ?? "Unknown")
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(k => k.Value)
                .Take(10)
                .ToListAsync();
        }
    }
}