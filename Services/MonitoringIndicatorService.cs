using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services
{
    public class MonitoringIndicatorService : IMonitoringIndicatorService
    {
        private const int ElderlyAge = 60;
        private const string CtsTargetScope = "CTSHIP";
        private readonly ApplicationDbContext _context;

        public MonitoringIndicatorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MonitoringDashboardViewModel> BuildDashboardAsync(
            string? state,
            CancellationToken cancellationToken = default)
        {
            return await BuildDashboardAsync(state, null, cancellationToken);
        }

        public async Task<MonitoringDashboardViewModel> BuildDashboardAsync(
            string? state,
            string? lga,
            CancellationToken cancellationToken = default)
        {
            string scope = string.IsNullOrWhiteSpace(state) ? CtsTargetScope : state.Trim();
            string selectedLga = scope == CtsTargetScope ? string.Empty : lga?.Trim() ?? string.Empty;
            IQueryable<Enrollee> query = _context.Enrollees.AsNoTracking();

            if (scope != CtsTargetScope)
            {
                query = query.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    query = query.Where(x => x.LGA == selectedLga);
                }
            }

            DateTime today = DateTime.Today;
            DateTime underFiveThreshold = today.AddYears(-5);
            DateTime elderlyThreshold = today.AddYears(-ElderlyAge);

            List<Enrollee> enrollees = await query.ToListAsync(cancellationToken);
            int total = enrollees.Count;
            int active = enrollees.Count(x =>
                string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase));
            int male = enrollees.Count(x =>
                string.Equals(x.Gender, "Male", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Gender, "M", StringComparison.OrdinalIgnoreCase));
            int female = enrollees.Count(x =>
                string.Equals(x.Gender, "Female", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Gender, "F", StringComparison.OrdinalIgnoreCase));

            int pregnant = enrollees.Count(x => x.IsPregnant && IsFemale(x));
            int underFive = enrollees.Count(x => x.DateOfBirth > underFiveThreshold);
            int elderly = enrollees.Count(x => x.DateOfBirth <= elderlyThreshold);
            int plwd = enrollees.Count(x => x.HasDisability);
            int other = enrollees.Count(x => x.IsIdp || !string.IsNullOrWhiteSpace(x.OtherVulnerableCategory));
            int vulnerable = enrollees.Count(x =>
                (x.IsPregnant && IsFemale(x))
                || x.DateOfBirth > underFiveThreshold
                || x.DateOfBirth <= elderlyThreshold
                || x.HasDisability
                || x.IsIdp
                || !string.IsNullOrWhiteSpace(x.OtherVulnerableCategory));

            int target = await _context.ProgramMonitoringTargets
                .AsNoTracking()
                .Where(x => x.Scope == scope
                    || (scope == CtsTargetScope && x.Scope == "National"))
                .OrderByDescending(x => x.Scope == scope)
                .Select(x => x.TargetEnrollees)
                .FirstOrDefaultAsync(cancellationToken);

            List<MonitoringCategoryViewModel> distribution = BuildExclusiveDistribution(
                enrollees,
                underFiveThreshold,
                elderlyThreshold);

            IQueryable<EncounterService> serviceQuery = _context.EncounterServices
                .AsNoTracking()
                .Where(x => x.Encounter != null);

            if (scope != CtsTargetScope)
            {
                serviceQuery = serviceQuery.Where(
                    x => x.Encounter!.Enrollee != null && x.Encounter.Enrollee.State == scope);

                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    string selectedLgaValue = selectedLga;
                    serviceQuery = serviceQuery.Where(
                        x => x.Encounter!.Enrollee != null && x.Encounter.Enrollee.LGA == selectedLgaValue);
                }
            }

            List<EncounterService> recordedServices = await serviceQuery.ToListAsync(cancellationToken);
            int immunization = CountDistinctEncounters(recordedServices, EncounterServiceCatalog.ImmunizationServices);
            int anc = CountDistinctEncounters(recordedServices, EncounterServiceCatalog.AncServices);
            int familyPlanning = CountDistinctEncounters(recordedServices, EncounterServiceCatalog.FamilyPlanningServices);
            int healthPromotion = CountDistinctEncounters(recordedServices, EncounterServiceCatalog.HealthPromotionServices);
            int otherPreventive = CountDistinctEncounters(
                recordedServices,
                EncounterServiceCatalog.OtherPreventiveServices);
            int totalPreventive = immunization + anc + familyPlanning + healthPromotion + otherPreventive;
            int totalRecordedServices = recordedServices.Count;

            IQueryable<Provider> providerQuery = _context.Providers.AsNoTracking().Where(x => x.IsActive);
            IQueryable<Encounter> encounterQuery = _context.Encounters.AsNoTracking();
            IQueryable<Claim> claimQuery = _context.Claims.AsNoTracking();
            IQueryable<DeathRegister> deathQuery = _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.Status == Models.Enums.DeathRegisterStatus.Audited);
            IQueryable<Referral> referralQuery = _context.Referrals
                .AsNoTracking()
                .Where(x => !x.IsDeleted);
            IQueryable<Complaint> complaintQuery = _context.Complaints.AsNoTracking();

            if (scope != CtsTargetScope)
            {
                IQueryable<Provider> scopedProviders = _context.Providers
                    .AsNoTracking()
                    .Where(x => x.State == scope);

                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    string selectedLgaValue = selectedLga;
                    scopedProviders = scopedProviders.Where(x =>
                        x.Enrollees.Any(enrollee =>
                            enrollee.State == scope && enrollee.LGA == selectedLgaValue));
                }

                providerQuery = providerQuery.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    string selectedLgaValue = selectedLga;
                    providerQuery = providerQuery.Where(x =>
                        x.Enrollees.Any(enrollee =>
                            enrollee.State == scope && enrollee.LGA == selectedLgaValue));
                }

                encounterQuery = encounterQuery.Where(x => x.Enrollee != null && x.Enrollee.State == scope);
                claimQuery = claimQuery.Where(x => x.Enrollee != null && x.Enrollee.State == scope);
                complaintQuery = complaintQuery.Where(x => x.State == scope);

                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    string selectedLgaValue = selectedLga;
                    encounterQuery = encounterQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                    claimQuery = claimQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                    complaintQuery = complaintQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                }

                List<int> scopedEnrolleeIds = enrollees.Select(x => x.Id).ToList();
                deathQuery = deathQuery.Where(
                    x => x.EnrolleeId.HasValue && scopedEnrolleeIds.Contains(x.EnrolleeId.Value));

                List<string> scopedProviderCodes = await scopedProviders
                    .Select(x => x.Code)
                    .ToListAsync(cancellationToken);
                List<string> scopedProviderNames = await scopedProviders
                    .Select(x => x.Name)
                    .ToListAsync(cancellationToken);
                referralQuery = referralQuery.Where(x =>
                    (x.FromProviderId != null && scopedProviderCodes.Contains(x.FromProviderId))
                    || scopedProviderNames.Contains(x.FromProviderName));
            }

            List<Claim> claims = await claimQuery.ToListAsync(cancellationToken);
            int paidClaims = claims.Count(x =>
                string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase));
            int pendingClaims = claims.Count(x =>
                string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, "Review Approved", StringComparison.OrdinalIgnoreCase));
            int rejectedClaims = claims.Count(x =>
                string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
            int totalEncounters = await encounterQuery.CountAsync(cancellationToken);
            List<Referral> referrals = await referralQuery.ToListAsync(cancellationToken);
            int completedReferrals = referrals.Count(IsCompletedReferral);
            int rejectedReferrals = referrals.Count(x => x.Status == ReferralStatus.Rejected);
            int pendingReferrals = Math.Max(
                0,
                referrals.Count - completedReferrals - rejectedReferrals);

            List<StateMonitoringViewModel> stateIndicators =
                await BuildStateIndicatorsAsync(scope, selectedLga, cancellationToken);

            List<string> availableStates = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State != "")
                .Select(x => x.State)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            return new MonitoringDashboardViewModel
            {
                Scope = scope,
                ScopeDisplay = BuildScopeDisplay(scope, selectedLga),
                SelectedState = scope == CtsTargetScope ? string.Empty : scope,
                SelectedLga = selectedLga,
                AvailableStates = availableStates,
                AvailableLgas = await GetAvailableLgasAsync(scope, cancellationToken),
                TargetEnrollees = target,
                TotalEnrolled = total,
                ActiveEnrollees = active,
                CoveragePercentage = Percentage(active, target),
                ActiveEnrolleeRate = Percentage(active, total),
                MaleCount = male,
                FemaleCount = female,
                OtherGenderCount = Math.Max(0, total - male - female),
                MalePercentage = Percentage(male, total),
                FemalePercentage = Percentage(female, total),
                PregnantWomenCount = pregnant,
                UnderFiveCount = underFive,
                PlwdCount = plwd,
                ElderlyCount = elderly,
                OtherVulnerableCount = other,
                VulnerableEnrolleeCount = vulnerable,
                PregnantWomenPercentage = Percentage(pregnant, total),
                UnderFivePercentage = Percentage(underFive, total),
                PlwdPercentage = Percentage(plwd, total),
                ElderlyPercentage = Percentage(elderly, total),
                OtherVulnerablePercentage = Percentage(other, total),
                VulnerablePopulationPercentage = Percentage(vulnerable, total),
                VulnerableDistribution = distribution,
                VulnerableDistributionTotal = distribution.Sum(x => x.Percentage),
                ImmunizationEncounters = immunization,
                AncEncounters = anc,
                FamilyPlanningEncounters = familyPlanning,
                HealthPromotionEncounters = healthPromotion,
                OtherPreventiveEncounters = otherPreventive,
                TotalPreventiveEncounters = totalPreventive,
                PreventiveCareRatePerThousand = RatePerThousand(totalPreventive, active),
                TotalProviders = await providerQuery.CountAsync(cancellationToken),
                TotalHmos = scope == CtsTargetScope
                    ? await _context.Hmos.AsNoTracking().CountAsync(cancellationToken)
                    : enrollees.Where(x => x.HmoId.HasValue).Select(x => x.HmoId).Distinct().Count(),
                TotalEncounters = totalEncounters,
                EncounterRatePerThousand = RatePerThousand(totalEncounters, active),
                TotalClaims = claims.Count,
                PaidClaims = paidClaims,
                PendingClaims = pendingClaims,
                RejectedClaims = rejectedClaims,
                ClaimApprovalRate = Percentage(paidClaims, claims.Count),
                TotalClaimValue = claims.Sum(x => x.Amount),
                PaidClaimValue = claims
                    .Where(x => string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount),
                AuditedDeaths = await deathQuery.CountAsync(cancellationToken),
                DeathRatePerThousand = RatePerThousand(
                    await deathQuery.CountAsync(cancellationToken),
                    active),
                TotalReferrals = referrals.Count,
                CompletedReferrals = completedReferrals,
                PendingReferrals = pendingReferrals,
                RejectedReferrals = rejectedReferrals,
                ReferralCompletionRate = Percentage(completedReferrals, referrals.Count),
                ComplaintMetrics = await ComplaintMetricsService.BuildAsync(
                    complaintQuery,
                    cancellationToken),
                StateIndicators = stateIndicators,
                MostUsedServices = recordedServices
                    .GroupBy(x => new { x.ServiceName, x.ServiceSetting })
                    .Select(x => new ServiceFrequencyViewModel
                    {
                        ServiceName = x.Key.ServiceName,
                        ServiceSetting = x.Key.ServiceSetting,
                        Frequency = x.Count(),
                        PercentageOfRecordedServices = Percentage(x.Count(), totalRecordedServices)
                    })
                    .OrderByDescending(x => x.Frequency)
                    .ThenBy(x => x.ServiceName)
                    .Take(15)
                    .ToList()
            };
        }

        private async Task<List<StateMonitoringViewModel>> BuildStateIndicatorsAsync(
            string scope,
            string selectedLga,
            CancellationToken cancellationToken)
        {
            IQueryable<Enrollee> enrollees = _context.Enrollees.AsNoTracking();
            if (scope != CtsTargetScope)
            {
                enrollees = enrollees.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    enrollees = enrollees.Where(x => x.LGA == selectedLga);
                }
            }

            var rows = await enrollees
                .Where(x => x.State != null && x.State != "")
                .GroupBy(x => x.State)
                .Select(x => new StateMonitoringViewModel
                {
                    State = x.Key,
                    TotalEnrollees = x.Count(),
                    ActiveEnrollees = x.Count(e => e.Status == "Active")
                })
                .OrderByDescending(x => x.TotalEnrollees)
                .ToListAsync(cancellationToken);

            foreach (StateMonitoringViewModel row in rows)
            {
                string state = row.State;
                IQueryable<Provider> providerQuery = _context.Providers
                    .AsNoTracking()
                    .Where(x => x.IsActive && x.State == state);
                IQueryable<Encounter> encounterQuery = _context.Encounters
                    .AsNoTracking()
                    .Where(x => x.Enrollee != null && x.Enrollee.State == state);
                IQueryable<Claim> claimQuery = _context.Claims
                    .AsNoTracking()
                    .Where(x => x.Enrollee != null && x.Enrollee.State == state);
                IQueryable<Complaint> complaintQuery = _context.Complaints
                    .AsNoTracking()
                    .Where(x => x.State == state);

                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    string selectedLgaValue = selectedLga;
                    providerQuery = providerQuery.Where(x =>
                        x.Enrollees.Any(enrollee =>
                            enrollee.State == state && enrollee.LGA == selectedLgaValue));
                    encounterQuery = encounterQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                    claimQuery = claimQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                    complaintQuery = complaintQuery.Where(x =>
                        x.Enrollee != null && x.Enrollee.LGA == selectedLgaValue);
                }

                row.Providers = await providerQuery.CountAsync(cancellationToken);
                row.Encounters = await encounterQuery.CountAsync(cancellationToken);

                List<Claim> stateClaims = await claimQuery.ToListAsync(cancellationToken);
                row.Claims = stateClaims.Count;
                row.PaidClaims = stateClaims.Count(x =>
                    string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase));
                row.ClaimValue = stateClaims.Sum(x => x.Amount);
                row.PaidClaimValue = stateClaims
                    .Where(x => string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);
                List<string> providerCodes = await providerQuery
                    .Select(x => x.Code)
                    .ToListAsync(cancellationToken);
                List<string> providerNames = await providerQuery
                    .Select(x => x.Name)
                    .ToListAsync(cancellationToken);
                List<Referral> stateReferrals = await _context.Referrals
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted
                        && ((x.FromProviderId != null && providerCodes.Contains(x.FromProviderId))
                            || providerNames.Contains(x.FromProviderName)))
                    .ToListAsync(cancellationToken);
                row.Referrals = stateReferrals.Count;
                row.CompletedReferrals = stateReferrals.Count(IsCompletedReferral);
                row.ReferralCompletionRate =
                    Percentage(row.CompletedReferrals, row.Referrals);
                ComplaintMetricsViewModel complaintMetrics =
                    await ComplaintMetricsService.BuildAsync(
                        complaintQuery,
                        cancellationToken);
                row.Complaints = complaintMetrics.TotalComplaints;
                row.OpenComplaints = complaintMetrics.OpenComplaints;
            }

            return rows;
        }

        private static List<MonitoringCategoryViewModel> BuildExclusiveDistribution(
            IEnumerable<Enrollee> enrollees,
            DateTime underFiveThreshold,
            DateTime elderlyThreshold)
        {
            var counts = new Dictionary<string, int>
            {
                ["Children Under 5"] = 0,
                ["Elderly (60+)"] = 0,
                ["Pregnant Women"] = 0,
                ["PLWD"] = 0,
                ["Other / IDP"] = 0
            };

            foreach (Enrollee enrollee in enrollees)
            {
                string? category = enrollee.DateOfBirth > underFiveThreshold ? "Children Under 5"
                    : enrollee.DateOfBirth <= elderlyThreshold ? "Elderly (60+)"
                    : enrollee.IsPregnant && IsFemale(enrollee) ? "Pregnant Women"
                    : enrollee.HasDisability ? "PLWD"
                    : enrollee.IsIdp || !string.IsNullOrWhiteSpace(enrollee.OtherVulnerableCategory)
                        ? "Other / IDP"
                        : null;

                if (category != null)
                {
                    counts[category]++;
                }
            }

            int totalVulnerable = counts.Values.Sum();
            return counts.Select(x => new MonitoringCategoryViewModel
            {
                Label = x.Key,
                Count = x.Value,
                Percentage = Percentage(x.Value, totalVulnerable)
            }).ToList();
        }

        private static decimal Percentage(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 100m, 1)
                : 0m;
        }

        private async Task<List<string>> GetAvailableLgasAsync(
            string state,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(state) || state == CtsTargetScope)
            {
                return new List<string>();
            }

            List<string> configured = NorthEastLocationData.GetLgas(state).ToList();
            List<string> recorded = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State == state && x.LGA != "")
                .Select(x => x.LGA)
                .Distinct()
                .ToListAsync(cancellationToken);

            return configured
                .Concat(recorded)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        private static string BuildScopeDisplay(string scope, string selectedLga)
        {
            if (scope == CtsTargetScope)
            {
                return CtsTargetScope;
            }

            return string.IsNullOrWhiteSpace(selectedLga)
                ? scope
                : $"{scope} / {selectedLga}";
        }

        private static bool IsCompletedReferral(Referral referral)
        {
            return referral.Status == ReferralStatus.Audited
                || referral.Status == ReferralStatus.Closed;
        }

        private static int CountDistinctEncounters(
            IEnumerable<EncounterService> services,
            IReadOnlySet<string> serviceNames)
        {
            return services
                .Where(x => serviceNames.Contains(x.ServiceName))
                .Select(x => x.EncounterId)
                .Distinct()
                .Count();
        }

        private static decimal RatePerThousand(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 1000m, 1)
                : 0m;
        }

        private static bool IsFemale(Enrollee enrollee)
        {
            return string.Equals(enrollee.Gender, "Female", StringComparison.OrdinalIgnoreCase)
                || string.Equals(enrollee.Gender, "F", StringComparison.OrdinalIgnoreCase);
        }
    }
}
