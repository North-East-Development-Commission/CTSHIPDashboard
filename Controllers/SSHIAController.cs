using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "SSHIA,CTSHIPAdmin,Admin")]
    public class SSHIAController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMonitoringIndicatorService _monitoringIndicatorService;

        public SSHIAController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMonitoringIndicatorService monitoringIndicatorService)
        {
            _context = context;
            _userManager = userManager;
            _monitoringIndicatorService = monitoringIndicatorService;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken = default)
        {
            string state = await ResolveStateScopeAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(state))
            {
                return Forbid();
            }

            DateTime startOfMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            IQueryable<Enrollee> stateEnrollees = _context.Enrollees.Where(enrollee => enrollee.State == state);
            IQueryable<Provider> stateProviders = _context.Providers.Where(provider => provider.State == state);
            IQueryable<Encounter> stateEncounters = _context.Encounters.Where(encounter => encounter.Enrollee != null && encounter.Enrollee.State == state);

            SSHIADashboardViewModel model = new()
            {
                StateName = state,
                TotalEnrollees = await stateEnrollees.CountAsync(cancellationToken),
                ActiveEnrollees = await stateEnrollees.CountAsync(enrollee => enrollee.Status == "Active", cancellationToken),
                NewEnrollmentsThisMonth = await stateEnrollees.CountAsync(enrollee => enrollee.DateRegistered >= startOfMonth, cancellationToken),
                TotalProviders = await stateProviders.CountAsync(cancellationToken),
                ActiveProviders = await stateProviders.CountAsync(provider => provider.IsActive, cancellationToken),
                PrimaryProviders = await stateProviders.CountAsync(provider => provider.Level == "Primary", cancellationToken),
                TotalHMOs = await _context.Hmos
                    .Where(hmo => hmo.Enrollees.Any(enrollee => enrollee.State == state))
                    .Select(hmo => hmo.Id)
                    .Distinct()
                    .CountAsync(cancellationToken),
                TotalEncounters = await stateEncounters.CountAsync(cancellationToken),
                TotalVisits = await stateEncounters.CountAsync(cancellationToken),
                UniqueServiceUsers = await stateEncounters
                    .Select(encounter => encounter.EnrolleeId)
                    .Distinct()
                    .CountAsync(cancellationToken),
                EncounterServicesRecorded = await _context.EncounterServices
                    .CountAsync(service => service.Encounter != null && service.Encounter.Enrollee != null && service.Encounter.Enrollee.State == state, cancellationToken),
                ComplaintMetrics = await ComplaintMetricsService.BuildAsync(
                    _context.Complaints.Where(complaint => complaint.State == state),
                    cancellationToken),
                Monitoring = await _monitoringIndicatorService.BuildDashboardAsync(state, cancellationToken: cancellationToken),
                RecentEnrollees = await stateEnrollees
                    .AsNoTracking()
                    .OrderByDescending(enrollee => enrollee.DateRegistered)
                    .Take(10)
                    .Select(enrollee => new EnrolleeSummaryViewModel
                    {
                        Id = enrollee.Id,
                        FullName = enrollee.FullName,
                        EnrollmentNumber = enrollee.EnrollmentNumber,
                        HmoName = enrollee.Hmo != null ? enrollee.Hmo.Name : "Not Assigned",
                        DateRegistered = enrollee.DateRegistered,
                        Status = enrollee.Status
                    })
                    .ToListAsync(cancellationToken)
            };

            int utilizationBase = model.ActiveEnrollees > 0 ? model.ActiveEnrollees : model.TotalEnrollees;
            model.ActiveEnrolleeRate = CalculateRate(model.ActiveEnrollees, model.TotalEnrollees);
            model.ServiceUtilizationRate = CalculateRate(model.UniqueServiceUsers, utilizationBase);
            model.EncounterRatePerThousand = CalculateRatePerThousand(model.TotalEncounters, utilizationBase);
            model.TopServices = await BuildTopServicesAsync(state, model, cancellationToken);
            model.ProgramActivities = BuildProgramActivities(model);

            return View(model);
        }

        private async Task<string> ResolveStateScopeAsync(CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (User.IsInRole("SSHIA"))
            {
                return user?.State?.Trim() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(user?.State))
            {
                return user.State.Trim();
            }

            return await _context.Enrollees
                .AsNoTracking()
                .Where(enrollee => enrollee.State != "")
                .Select(enrollee => enrollee.State)
                .OrderBy(state => state)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        private async Task<List<ServiceFrequencyViewModel>> BuildTopServicesAsync(
            string state,
            SSHIADashboardViewModel model,
            CancellationToken cancellationToken)
        {
            List<ServiceFrequencyViewModel> services = await _context.EncounterServices
                .AsNoTracking()
                .Where(service => service.Encounter != null
                    && service.Encounter.Enrollee != null
                    && service.Encounter.Enrollee.State == state)
                .GroupBy(service => new { service.ServiceName, service.ServiceSetting })
                .Select(group => new ServiceFrequencyViewModel
                {
                    ServiceName = group.Key.ServiceName,
                    ServiceSetting = group.Key.ServiceSetting,
                    Frequency = group.Count()
                })
                .OrderByDescending(service => service.Frequency)
                .ThenBy(service => service.ServiceName)
                .Take(8)
                .ToListAsync(cancellationToken);

            int serviceBase = model.EncounterServicesRecorded;
            if (services.Count == 0)
            {
                services = await _context.Encounters
                    .AsNoTracking()
                    .Where(encounter => encounter.Enrollee != null && encounter.Enrollee.State == state)
                    .GroupBy(encounter => encounter.ServiceSetting)
                    .Select(group => new ServiceFrequencyViewModel
                    {
                        ServiceName = group.Key,
                        ServiceSetting = group.Key,
                        Frequency = group.Count()
                    })
                    .OrderByDescending(service => service.Frequency)
                    .ThenBy(service => service.ServiceName)
                    .Take(8)
                    .ToListAsync(cancellationToken);

                serviceBase = model.TotalEncounters;
            }

            foreach (ServiceFrequencyViewModel service in services)
            {
                service.PercentageOfRecordedServices = CalculateRate(service.Frequency, serviceBase);
            }

            return services;
        }

        private static List<SSHIAProgramActivityRow> BuildProgramActivities(SSHIADashboardViewModel model)
        {
            return new List<SSHIAProgramActivityRow>
            {
                new()
                {
                    Activity = "Enrollment coverage",
                    Count = model.TotalEnrollees,
                    Rate = model.ActiveEnrolleeRate,
                    Note = $"{model.ActiveEnrollees:N0} active enrollees"
                },
                new()
                {
                    Activity = "Provider availability",
                    Count = model.TotalProviders,
                    Rate = CalculateRate(model.ActiveProviders, model.TotalProviders),
                    Note = $"{model.PrimaryProviders:N0} primary providers"
                },
                new()
                {
                    Activity = "Service utilization",
                    Count = model.TotalEncounters,
                    Rate = model.ServiceUtilizationRate,
                    Note = $"{model.UniqueServiceUsers:N0} unique service users"
                },
                new()
                {
                    Activity = "Complaint resolution",
                    Count = model.ComplaintMetrics.TotalComplaints,
                    Rate = model.ComplaintMetrics.ResolutionRate,
                    Note = $"{model.ComplaintMetrics.CriticalComplaints:N0} critical active complaints"
                },
                new()
                {
                    Activity = "HMO participation",
                    Count = model.TotalHMOs,
                    Rate = 0m,
                    Note = "Distinct HMOs with enrollees in state"
                },
                new()
                {
                    Activity = "Monitoring coverage",
                    Count = model.Monitoring.TargetEnrollees,
                    Rate = model.Monitoring.CoveragePercentage,
                    Note = model.Monitoring.TargetEnrollees > 0
                        ? $"{model.Monitoring.TotalEnrolled:N0} of {model.Monitoring.TargetEnrollees:N0} target enrollees"
                        : "No state target configured yet"
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
    }
}