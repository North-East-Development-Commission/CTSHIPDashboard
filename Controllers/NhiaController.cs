using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "NHIA,CTSHIPAdmin,Admin,Monitoring,NEDCAdmin")]
    public class NHIAController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NHIAController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> Dashboard()
        {
            DateTime startOfMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            NHIADashboardViewModel vm = new();

            vm.TotalEnrollees = await _context.Enrollees.CountAsync();
            vm.ActiveEnrollees = await _context.Enrollees.CountAsync(e => e.Status == "Active");
            vm.NewEnrollmentsThisMonth = await _context.Enrollees.CountAsync(e => e.DateRegistered >= startOfMonth);
            vm.ActiveEnrolleeRate = CalculateRate(vm.ActiveEnrollees, vm.TotalEnrollees);

            vm.TotalClaims = await _context.Claims.CountAsync();
            vm.PendingClaims = await _context.Claims.CountAsync(c => c.Status == "Submitted");
            vm.ApprovedClaims = await _context.Claims.CountAsync(c => c.Status == "Approved");
            vm.PaidClaims = await _context.Claims.CountAsync(c => c.Status == "Paid");
            vm.RejectedClaims = await _context.Claims.CountAsync(c => c.Status == "Rejected");
            vm.TotalClaimAmount = await _context.Claims.Select(c => (decimal?)c.Amount).SumAsync() ?? 0m;
            vm.PaidClaimAmount = await _context.Claims
                .Where(c => c.Status == "Paid")
                .Select(c => (decimal?)c.Amount)
                .SumAsync() ?? 0m;
            vm.ClaimPaymentRate = CalculateRate(vm.PaidClaims, vm.TotalClaims);

            vm.TotalHMOs = await _context.Hmos.CountAsync();
            vm.ActiveHMOs = await _context.Hmos.CountAsync(h => h.Status == "Active");
            vm.HmosWithProviders = await _context.Providers
                .Where(p => p.HmoId > 0)
                .Select(p => p.HmoId)
                .Distinct()
                .CountAsync();

            vm.TotalProviders = await _context.Providers.CountAsync();
            vm.ActiveProviders = await _context.Providers.CountAsync(p => p.IsActive);
            vm.ProvidersWithEncounters = await _context.Encounters
                .Select(e => e.ProviderId)
                .Distinct()
                .CountAsync();
            vm.ProviderActivityRate = CalculateRate(vm.ProvidersWithEncounters, vm.TotalProviders);

            vm.TotalEncounters = await _context.Encounters.CountAsync();
            vm.UniqueServiceUsers = await _context.Encounters
                .Select(e => e.EnrolleeId)
                .Distinct()
                .CountAsync();
            vm.EncounterServicesRecorded = await _context.EncounterServices.CountAsync();

            int utilizationBase = vm.ActiveEnrollees > 0 ? vm.ActiveEnrollees : vm.TotalEnrollees;
            vm.ServiceUtilizationRate = CalculateRate(vm.UniqueServiceUsers, utilizationBase);
            vm.EncounterRatePerThousand = CalculateRatePerThousand(vm.TotalEncounters, utilizationBase);

            vm.ComplaintMetrics = await ComplaintMetricsService.BuildAsync(_context.Complaints);
            vm.TopServices = await BuildTopServicesAsync(vm);
            vm.StateSummaries = await BuildStateSummariesAsync();
            vm.OversightSignals = BuildOversightSignals(vm);

            vm.RecentEnrollees = await _context.Enrollees
                .AsNoTracking()
                .Include(e => e.Hmo)
                .OrderByDescending(e => e.DateRegistered)
                .Take(10)
                .Select(e => new RecentEnrollee
                {
                    EnrollmentNumber = e.EnrollmentNumber,
                    FullName = e.FullName,
                    HmoName = e.Hmo != null ? e.Hmo.Name : "N/A",
                    State = e.State,
                    DateRegistered = e.DateRegistered,
                    Status = e.Status ?? "Active"
                })
                .ToListAsync();

            vm.RecentClaims = await _context.Claims
                .AsNoTracking()
                .Include(c => c.Enrollee)
                .Include(c => c.Provider)
                .OrderByDescending(c => c.DateSubmitted)
                .Take(10)
                .Select(c => new RecentClaimSummary
                {
                    ClaimNumber = c.ClaimNumber,
                    EnrolleeName = c.Enrollee != null ? c.Enrollee.FullName : "N/A",
                    ProviderName = c.Provider != null ? c.Provider.Name : "N/A",
                    Amount = c.Amount,
                    Status = c.Status,
                    DateSubmitted = c.DateSubmitted
                })
                .ToListAsync();

            return View(vm);
        }

        private async Task<List<ServiceFrequencyViewModel>> BuildTopServicesAsync(NHIADashboardViewModel vm)
        {
            List<ServiceFrequencyViewModel> topServices = await _context.EncounterServices
                .AsNoTracking()
                .GroupBy(service => new { service.ServiceName, service.ServiceSetting })
                .Select(group => new ServiceFrequencyViewModel
                {
                    ServiceName = group.Key.ServiceName,
                    ServiceSetting = group.Key.ServiceSetting,
                    Frequency = group.Count()
                })
                .OrderByDescending(service => service.Frequency)
                .ThenBy(service => service.ServiceName)
                .Take(6)
                .ToListAsync();

            int serviceBase = vm.EncounterServicesRecorded;
            if (topServices.Count == 0)
            {
                topServices = await _context.Encounters
                    .AsNoTracking()
                    .GroupBy(encounter => encounter.ServiceSetting)
                    .Select(group => new ServiceFrequencyViewModel
                    {
                        ServiceName = group.Key,
                        ServiceSetting = group.Key,
                        Frequency = group.Count()
                    })
                    .OrderByDescending(service => service.Frequency)
                    .ThenBy(service => service.ServiceName)
                    .Take(6)
                    .ToListAsync();

                serviceBase = vm.TotalEncounters;
            }

            foreach (ServiceFrequencyViewModel service in topServices)
            {
                service.PercentageOfRecordedServices = CalculateRate(service.Frequency, serviceBase);
            }

            return topServices;
        }

        private async Task<List<StateSummary>> BuildStateSummariesAsync()
        {
            var enrollmentGroups = await _context.Enrollees
                .AsNoTracking()
                .GroupBy(enrollee => enrollee.State)
                .Select(group => new
                {
                    State = group.Key,
                    Count = group.Count(),
                    Active = group.Count(enrollee => enrollee.Status == "Active")
                })
                .ToListAsync();

            var claimGroups = await _context.Claims
                .AsNoTracking()
                .GroupBy(claim => claim.Enrollee!.State)
                .Select(group => new
                {
                    State = group.Key,
                    Count = group.Count(),
                    Amount = group.Sum(claim => claim.Amount)
                })
                .ToListAsync();

            var providerGroups = await _context.Providers
                .AsNoTracking()
                .GroupBy(provider => provider.State)
                .Select(group => new { State = group.Key, Count = group.Count() })
                .ToListAsync();

            var hmoGroups = await _context.Hmos
                .AsNoTracking()
                .GroupBy(hmo => hmo.State)
                .Select(group => new { State = group.Key, Count = group.Count() })
                .ToListAsync();

            var encounterGroups = await _context.Encounters
                .AsNoTracking()
                .GroupBy(encounter => encounter.Enrollee!.State)
                .Select(group => new
                {
                    State = group.Key,
                    Count = group.Count(),
                    UniqueEnrollees = group.Select(encounter => encounter.EnrolleeId).Distinct().Count()
                })
                .ToListAsync();

            var complaintGroups = await _context.Complaints
                .AsNoTracking()
                .GroupBy(complaint => complaint.State)
                .Select(group => new { State = group.Key, Count = group.Count() })
                .ToListAsync();

            var enrollmentByState = enrollmentGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(
                    group => group.Key,
                    group => new { Count = group.Sum(item => item.Count), Active = group.Sum(item => item.Active) });

            var claimsByState = claimGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(
                    group => group.Key,
                    group => new { Count = group.Sum(item => item.Count), Amount = group.Sum(item => item.Amount) });

            var providersByState = providerGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

            var hmosByState = hmoGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

            var encountersByState = encounterGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(
                    group => group.Key,
                    group => new { Count = group.Sum(item => item.Count), UniqueEnrollees = group.Sum(item => item.UniqueEnrollees) });

            var complaintsByState = complaintGroups
                .GroupBy(item => NormalizeState(item.State))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

            IEnumerable<string> states = enrollmentByState.Keys
                .Union(claimsByState.Keys)
                .Union(providersByState.Keys)
                .Union(hmosByState.Keys)
                .Union(encountersByState.Keys)
                .Union(complaintsByState.Keys)
                .OrderBy(state => state);

            return states
                .Select(state =>
                {
                    enrollmentByState.TryGetValue(state, out var enrollment);
                    claimsByState.TryGetValue(state, out var claims);
                    providersByState.TryGetValue(state, out int providers);
                    hmosByState.TryGetValue(state, out int hmos);
                    encountersByState.TryGetValue(state, out var encounters);
                    complaintsByState.TryGetValue(state, out int complaints);

                    int enrollees = enrollment?.Count ?? 0;
                    int uniqueServiceUsers = encounters?.UniqueEnrollees ?? 0;

                    return new StateSummary
                    {
                        StateName = state,
                        Enrollees = enrollees,
                        ActiveEnrollees = enrollment?.Active ?? 0,
                        Claims = claims?.Count ?? 0,
                        Providers = providers,
                        Hmos = hmos,
                        Encounters = encounters?.Count ?? 0,
                        Complaints = complaints,
                        ClaimAmount = claims?.Amount ?? 0m,
                        UtilizationRate = CalculateRate(uniqueServiceUsers, enrollees)
                    };
                })
                .OrderByDescending(summary => summary.Enrollees)
                .ThenBy(summary => summary.StateName)
                .Take(12)
                .ToList();
        }

        private static List<ProgramOversightSignal> BuildOversightSignals(NHIADashboardViewModel vm)
        {
            return new List<ProgramOversightSignal>
            {
                new()
                {
                    Title = "Enrollment health",
                    Value = $"{vm.ActiveEnrolleeRate:N1}%",
                    Detail = $"{vm.ActiveEnrollees:N0} active of {vm.TotalEnrollees:N0} enrollees",
                    IconCss = "bi-person-check-fill",
                    ToneCss = vm.ActiveEnrolleeRate >= 80m ? "text-success" : "text-warning"
                },
                new()
                {
                    Title = "Claim payment progress",
                    Value = $"{vm.ClaimPaymentRate:N1}%",
                    Detail = $"{vm.PaidClaims:N0} paid; {vm.PendingClaims + vm.ApprovedClaims:N0} awaiting closure",
                    IconCss = "bi-receipt-cutoff",
                    ToneCss = vm.ClaimPaymentRate >= 70m ? "text-success" : "text-warning"
                },
                new()
                {
                    Title = "Provider activity",
                    Value = $"{vm.ProviderActivityRate:N1}%",
                    Detail = $"{vm.ProvidersWithEncounters:N0} of {vm.TotalProviders:N0} providers have encounters",
                    IconCss = "bi-hospital-fill",
                    ToneCss = vm.ProviderActivityRate >= 70m ? "text-success" : "text-warning"
                },
                new()
                {
                    Title = "Complaint resolution",
                    Value = $"{vm.ComplaintMetrics.ResolutionRate:N1}%",
                    Detail = $"{vm.ComplaintMetrics.OpenComplaints + vm.ComplaintMetrics.InProgressComplaints:N0} open or in progress",
                    IconCss = "bi-chat-square-text-fill",
                    ToneCss = vm.ComplaintMetrics.CriticalComplaints == 0 ? "text-success" : "text-danger"
                }
            };
        }

        private static decimal CalculateRate(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 100m, 1)
                : 0m;
        }

        private static decimal CalculateRatePerThousand(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 1000m, 1)
                : 0m;
        }

        private static string NormalizeState(string? state)
        {
            return string.IsNullOrWhiteSpace(state) ? "Unspecified" : state.Trim();
        }
    }
}
