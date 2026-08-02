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
            return await BuildDashboardAsync(state, null, null, cancellationToken);
        }

        public async Task<MonitoringDashboardViewModel> BuildDashboardAsync(
            string? state,
            string? lga,
            CancellationToken cancellationToken = default)
        {
            return await BuildDashboardAsync(state, lga, null, cancellationToken);
        }

        public async Task<MonitoringDashboardViewModel> BuildDashboardAsync(
            string? state,
            string? lga,
            int? hmoId,
            CancellationToken cancellationToken = default)
        {
            string scope = string.IsNullOrWhiteSpace(state) ? CtsTargetScope : state.Trim();
            string selectedLga = scope == CtsTargetScope ? string.Empty : lga?.Trim() ?? string.Empty;
            int? selectedHmoId = hmoId.HasValue && hmoId.Value > 0 ? hmoId.Value : null;
            string? selectedHmoName = null;
            string? selectedHmoCode = null;

            if (selectedHmoId.HasValue)
            {
                var selectedHmo = await _context.Hmos
                    .AsNoTracking()
                    .Where(x => x.Id == selectedHmoId.Value)
                    .Select(x => new { x.Id, x.Name, x.RegistrationNumber })
                    .FirstOrDefaultAsync(cancellationToken);

                if (selectedHmo == null)
                {
                    selectedHmoId = null;
                }
                else
                {
                    selectedHmoName = selectedHmo.Name;
                    selectedHmoCode = selectedHmo.RegistrationNumber;
                }
            }

            IQueryable<Enrollee> query = _context.Enrollees.AsNoTracking();

            if (scope != CtsTargetScope)
            {
                query = query.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    query = query.Where(x => x.LGA == selectedLga);
                }
            }

            if (selectedHmoId.HasValue)
            {
                int hmoFilter = selectedHmoId.Value;
                query = query.Where(x => x.HmoId == hmoFilter);
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

            if (selectedHmoId.HasValue)
            {
                int hmoFilter = selectedHmoId.Value;
                serviceQuery = serviceQuery.Where(
                    x => x.Encounter!.Enrollee != null && x.Encounter.Enrollee.HmoId == hmoFilter);
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

            if (selectedHmoId.HasValue)
            {
                int hmoFilter = selectedHmoId.Value;
                providerQuery = providerQuery.Where(x => x.HmoId == hmoFilter);
                encounterQuery = encounterQuery.Where(x => x.Enrollee != null && x.Enrollee.HmoId == hmoFilter);
                claimQuery = claimQuery.Where(x => x.HmoId == hmoFilter || (x.Enrollee != null && x.Enrollee.HmoId == hmoFilter));
                complaintQuery = complaintQuery.Where(x => x.HmoId == hmoFilter);

                List<int> filteredEnrolleeIds = enrollees.Select(x => x.Id).ToList();
                deathQuery = deathQuery.Where(x => x.EnrolleeId.HasValue && filteredEnrolleeIds.Contains(x.EnrolleeId.Value));

                if (!string.IsNullOrWhiteSpace(selectedHmoCode) && !string.IsNullOrWhiteSpace(selectedHmoName))
                {
                    referralQuery = referralQuery.Where(x => x.HmoCode == selectedHmoCode || x.HmoName == selectedHmoName);
                }
                else if (!string.IsNullOrWhiteSpace(selectedHmoCode))
                {
                    referralQuery = referralQuery.Where(x => x.HmoCode == selectedHmoCode);
                }
                else if (!string.IsNullOrWhiteSpace(selectedHmoName))
                {
                    referralQuery = referralQuery.Where(x => x.HmoName == selectedHmoName);
                }
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
            int uniqueServiceUsers = await encounterQuery
                .Select(x => x.EnrolleeId)
                .Distinct()
                .CountAsync(cancellationToken);
            int totalProviders = await providerQuery.CountAsync(cancellationToken);
            int primaryProviders = await providerQuery.CountAsync(
                x => x.Level == "Primary",
                cancellationToken);
            int secondaryProviders = await providerQuery.CountAsync(
                x => x.Level == "Secondary",
                cancellationToken);
            int referralProviders = await providerQuery.CountAsync(
                x => x.Level == "Referral Hospital" || x.Code.StartsWith("REF-"),
                cancellationToken);
            int totalHmos = selectedHmoId.HasValue
                ? 1
                : scope == CtsTargetScope
                    ? await _context.Hmos.AsNoTracking().CountAsync(cancellationToken)
                    : enrollees.Where(x => x.HmoId.HasValue).Select(x => x.HmoId).Distinct().Count();

            IQueryable<StateOfficeMonthlyReport> reportQuery = _context.StateOfficeMonthlyReports.AsNoTracking();
            if (scope != CtsTargetScope)
            {
                reportQuery = reportQuery.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    reportQuery = reportQuery.Where(x => x.Lga == selectedLga);
                }
            }

            if (selectedHmoId.HasValue)
            {
                int hmoFilter = selectedHmoId.Value;
                reportQuery = reportQuery.Where(x => _context.Providers.Any(provider =>
                    provider.Id == x.ProviderId && provider.HmoId == hmoFilter));
            }

            int totalReports = await reportQuery.CountAsync(cancellationToken);
            int pendingReports = await reportQuery.CountAsync(x => x.AuditStatus == "Pending", cancellationToken);
            int auditedReports = await reportQuery.CountAsync(x => x.AuditStatus == "Audited", cancellationToken);
            int reportsNeedingCorrection = await reportQuery.CountAsync(x => x.AuditStatus == "Needs Correction", cancellationToken);
            DateTime activeUserCutoff = DateTime.UtcNow.AddDays(-30);
            IQueryable<ApplicationUser> userQuery = _context.Users.AsNoTracking();
            IQueryable<UserActivity> activeUserQuery = _context.UserActivities
                .AsNoTracking()
                .Where(x => x.Timestamp >= activeUserCutoff);

            if (selectedHmoId.HasValue)
            {
                int hmoFilter = selectedHmoId.Value;
                userQuery = userQuery.Where(x => x.HmoId == hmoFilter);
                activeUserQuery = activeUserQuery.Where(x => _context.Users.Any(user =>
                    user.Id == x.UserId && user.HmoId == hmoFilter));
            }

            int totalUsers = await userQuery.CountAsync(cancellationToken);
            int activeUsersLast30Days = await activeUserQuery
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            ComplaintMetricsViewModel complaintMetrics = await ComplaintMetricsService.BuildAsync(
                complaintQuery,
                cancellationToken);
            CapitationSummaryViewModel capitation = await BuildCapitationSummaryAsync(scope, selectedLga, selectedHmoId, cancellationToken);
            List<ProviderLevelMetricViewModel> providerLevelMetrics = await BuildProviderLevelMetricsAsync(
                providerQuery,
                encounterQuery,
                scope,
                selectedLga,
                selectedHmoId,
                cancellationToken);
            List<HmoOversightRowViewModel> hmoOversight = await BuildHmoOversightAsync(
                scope,
                selectedLga,
                selectedHmoId,
                cancellationToken);
            List<DiseaseTrendViewModel> diseaseTrends = await BuildDiseaseTrendsAsync(
                encounterQuery,
                totalEncounters,
                cancellationToken);

            List<Referral> referrals = await referralQuery.ToListAsync(cancellationToken);
            int completedReferrals = referrals.Count(IsCompletedReferral);
            int rejectedReferrals = referrals.Count(x => x.Status == ReferralStatus.Rejected);
            int pendingReferrals = Math.Max(
                0,
                referrals.Count - completedReferrals - rejectedReferrals);

            List<StateMonitoringViewModel> stateIndicators =
                await BuildStateIndicatorsAsync(scope, selectedLga, selectedHmoId, cancellationToken);

            EncounterDemographicMatrixViewModel encounterDemographicMatrix =
                await EncounterDemographicMatrixService.BuildAsync(
                    query,
                    encounterQuery,
                    BuildScopeDisplay(scope, selectedLga, selectedHmoName),
                    cancellationToken);

            List<string> availableStates = await _context.Enrollees
                .AsNoTracking()
                .Where(x => x.State != "")
                .Select(x => x.State)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            MonitoringDashboardViewModel dashboard = new()
            {
                Scope = scope,
                ScopeDisplay = BuildScopeDisplay(scope, selectedLga, selectedHmoName),
                SelectedState = scope == CtsTargetScope ? string.Empty : scope,
                SelectedLga = selectedLga,
                SelectedHmoId = selectedHmoId,
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
                UniqueServiceUsers = uniqueServiceUsers,
                TotalEncounterServices = totalRecordedServices,
                ServiceUtilizationRate = Percentage(uniqueServiceUsers, active),
                TotalProviders = totalProviders,
                PrimaryProviders = primaryProviders,
                SecondaryProviders = secondaryProviders,
                ReferralProviders = referralProviders,
                TotalHmos = totalHmos,
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
                TotalUsers = totalUsers,
                ActiveUsersLast30Days = activeUsersLast30Days,
                TotalReports = totalReports,
                PendingReports = pendingReports,
                AuditedReports = auditedReports,
                ReportsNeedingCorrection = reportsNeedingCorrection,
                Capitation = capitation,
                ComplaintMetrics = complaintMetrics,
                HmoOversight = hmoOversight,
                ProviderLevelMetrics = providerLevelMetrics,
                DiseaseTrends = diseaseTrends,
                StateIndicators = stateIndicators,
                EncounterDemographicMatrix = encounterDemographicMatrix,
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

            dashboard.SystemActivityMatrix = BuildSystemActivityMatrix(dashboard);
            return dashboard;
        }

        private async Task<CapitationSummaryViewModel> BuildCapitationSummaryAsync(
            string scope,
            string selectedLga,
            int? hmoId,
            CancellationToken cancellationToken)
        {
            IQueryable<CapitationPayment> query = _context.CapitationPayments
                .AsNoTracking()
                .Where(x => x.Provider != null);

            if (scope != CtsTargetScope)
            {
                query = query.Where(x => x.Provider!.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    query = query.Where(x => x.Provider!.LGA == selectedLga);
                }
            }

            if (hmoId.HasValue)
            {
                int hmoFilter = hmoId.Value;
                query = query.Where(x => x.Provider!.HmoId == hmoFilter);
            }

            List<CapitationPayment> payments = await query.ToListAsync(cancellationToken);
            decimal totalAmount = payments.Sum(x => x.EnrolleeCount * x.CapitationPerEnrollee);
            List<CapitationPayment> paid = payments
                .Where(x => string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new CapitationSummaryViewModel
            {
                TotalPayments = payments.Count,
                PaidPayments = paid.Count,
                PendingPayments = payments.Count(x => !string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)),
                TotalAmount = totalAmount,
                PaidAmount = paid.Sum(x => x.EnrolleeCount * x.CapitationPerEnrollee),
                AverageUtilizationRate = payments.Count > 0
                    ? Math.Round(payments.Average(x => x.UtilizationRate), 1)
                    : 0m
            };
        }

        private async Task<List<ProviderLevelMetricViewModel>> BuildProviderLevelMetricsAsync(
            IQueryable<Provider> providerQuery,
            IQueryable<Encounter> encounterQuery,
            string scope,
            string selectedLga,
            int? hmoId,
            CancellationToken cancellationToken)
        {
            List<Provider> providers = await providerQuery
                .OrderBy(x => x.Level)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            if (providers.Count == 0)
            {
                return new List<ProviderLevelMetricViewModel>();
            }

            List<int> providerIds = providers.Select(x => x.Id).ToList();
            Dictionary<int, int> encounterCounts = await encounterQuery
                .Where(x => providerIds.Contains(x.ProviderId))
                .GroupBy(x => x.ProviderId)
                .Select(x => new { ProviderId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.ProviderId, x => x.Count, cancellationToken);

            IQueryable<Enrollee> enrolleeQuery = _context.Enrollees.AsNoTracking();
            if (scope != CtsTargetScope)
            {
                enrolleeQuery = enrolleeQuery.Where(x => x.State == scope);
                if (!string.IsNullOrWhiteSpace(selectedLga))
                {
                    enrolleeQuery = enrolleeQuery.Where(x => x.LGA == selectedLga);
                }
            }

            if (hmoId.HasValue)
            {
                int hmoFilter = hmoId.Value;
                enrolleeQuery = enrolleeQuery.Where(x => x.HmoId == hmoFilter);
            }

            Dictionary<int, int> enrolleeCounts = await enrolleeQuery
                .Where(x => x.ProviderId.HasValue && providerIds.Contains(x.ProviderId.Value))
                .GroupBy(x => x.ProviderId!.Value)
                .Select(x => new { ProviderId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.ProviderId, x => x.Count, cancellationToken);

            return providers
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Level) ? "Unspecified" : x.Level)
                .Select(group =>
                {
                    List<int> ids = group.Select(x => x.Id).ToList();
                    int enrollees = ids.Sum(id => enrolleeCounts.TryGetValue(id, out int count) ? count : 0);
                    int encounters = ids.Sum(id => encounterCounts.TryGetValue(id, out int count) ? count : 0);
                    return new ProviderLevelMetricViewModel
                    {
                        Level = group.Key,
                        Providers = group.Count(),
                        Enrollees = enrollees,
                        Encounters = encounters,
                        EncounterRatePerThousand = RatePerThousand(encounters, enrollees)
                    };
                })
                .OrderByDescending(x => x.Providers)
                .ThenBy(x => x.Level)
                .ToList();
        }

        private async Task<List<HmoOversightRowViewModel>> BuildHmoOversightAsync(
            string scope,
            string selectedLga,
            int? hmoId,
            CancellationToken cancellationToken)
        {
            IQueryable<Hmo> hmoQuery = _context.Hmos.AsNoTracking();
            if (hmoId.HasValue)
            {
                int hmoFilter = hmoId.Value;
                hmoQuery = hmoQuery.Where(x => x.Id == hmoFilter);
            }

            List<Hmo> hmos = await hmoQuery
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            List<HmoOversightRowViewModel> rows = new();
            foreach (Hmo hmo in hmos)
            {
                IQueryable<Enrollee> enrollees = _context.Enrollees
                    .AsNoTracking()
                    .Where(x => x.HmoId == hmo.Id);
                IQueryable<Provider> providers = _context.Providers
                    .AsNoTracking()
                    .Where(x => x.HmoId == hmo.Id);
                IQueryable<Encounter> encounters = _context.Encounters
                    .AsNoTracking()
                    .Where(x => x.Enrollee != null && x.Enrollee.HmoId == hmo.Id);
                IQueryable<Claim> claims = _context.Claims
                    .AsNoTracking()
                    .Where(x => x.HmoId == hmo.Id);
                IQueryable<Complaint> complaints = _context.Complaints
                    .AsNoTracking()
                    .Where(x => x.HmoId == hmo.Id);

                if (scope != CtsTargetScope)
                {
                    enrollees = enrollees.Where(x => x.State == scope);
                    providers = providers.Where(x => x.State == scope);
                    encounters = encounters.Where(x => x.Enrollee != null && x.Enrollee.State == scope);
                    claims = claims.Where(x => x.Enrollee != null && x.Enrollee.State == scope);
                    complaints = complaints.Where(x => x.State == scope);

                    if (!string.IsNullOrWhiteSpace(selectedLga))
                    {
                        enrollees = enrollees.Where(x => x.LGA == selectedLga);
                        providers = providers.Where(x => x.LGA == selectedLga);
                        encounters = encounters.Where(x => x.Enrollee != null && x.Enrollee.LGA == selectedLga);
                        claims = claims.Where(x => x.Enrollee != null && x.Enrollee.LGA == selectedLga);
                        complaints = complaints.Where(x => x.Enrollee != null && x.Enrollee.LGA == selectedLga);
                    }
                }

                int enrolleeCount = await enrollees.CountAsync(cancellationToken);
                if (scope != CtsTargetScope && enrolleeCount == 0 && !await providers.AnyAsync(cancellationToken))
                {
                    continue;
                }

                int activeEnrollees = await enrollees.CountAsync(x => x.Status == "Active", cancellationToken);
                int uniqueServiceUsers = await encounters
                    .Select(x => x.EnrolleeId)
                    .Distinct()
                    .CountAsync(cancellationToken);

                rows.Add(new HmoOversightRowViewModel
                {
                    Id = hmo.Id,
                    Name = hmo.Name,
                    States = hmo.State,
                    Enrollees = enrolleeCount,
                    ActiveEnrollees = activeEnrollees,
                    Providers = await providers.CountAsync(cancellationToken),
                    Encounters = await encounters.CountAsync(cancellationToken),
                    Claims = await claims.CountAsync(cancellationToken),
                    ClaimValue = await claims.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m,
                    Complaints = await complaints.CountAsync(cancellationToken),
                    ServiceUtilizationRate = Percentage(uniqueServiceUsers, activeEnrollees)
                });
            }

            return rows
                .OrderByDescending(x => x.Enrollees)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private async Task<List<DiseaseTrendViewModel>> BuildDiseaseTrendsAsync(
            IQueryable<Encounter> encounterQuery,
            int totalEncounters,
            CancellationToken cancellationToken)
        {
            var trends = await encounterQuery
                .Where(x => x.Diagnosis != null && x.Diagnosis != "")
                .GroupBy(x => x.Diagnosis!.Trim())
                .Select(x => new
                {
                    Diagnosis = x.Key,
                    Encounters = x.Count()
                })
                .OrderByDescending(x => x.Encounters)
                .ThenBy(x => x.Diagnosis)
                .Take(12)
                .ToListAsync(cancellationToken);

            return trends
                .Select(x => new DiseaseTrendViewModel
                {
                    Diagnosis = x.Diagnosis,
                    Encounters = x.Encounters,
                    Percentage = Percentage(x.Encounters, totalEncounters)
                })
                .ToList();
        }

        private static List<SystemActivityMatrixRowViewModel> BuildSystemActivityMatrix(
            MonitoringDashboardViewModel dashboard)
        {
            return new List<SystemActivityMatrixRowViewModel>
            {
                new() { Area = "Enrollment", Indicator = "Active enrollee rate", Count = dashboard.TotalEnrolled, Rate = dashboard.ActiveEnrolleeRate, DecisionSignal = $"{dashboard.ActiveEnrollees:N0} active enrollees" },
                new() { Area = "HMO", Indicator = "HMO participation", Count = dashboard.TotalHmos, Rate = dashboard.HmoOversight.Count > 0 ? Math.Round(dashboard.HmoOversight.Average(x => x.ServiceUtilizationRate), 1) : 0m, DecisionSignal = "Review HMO enrollee distribution and utilization" },
                new() { Area = "Primary Provider", Indicator = "Primary facility network", Count = dashboard.PrimaryProviders, Rate = Percentage(dashboard.PrimaryProviders, dashboard.TotalProviders), DecisionSignal = "Primary care delivery capacity" },
                new() { Area = "Secondary Provider", Indicator = "Secondary facility network", Count = dashboard.SecondaryProviders, Rate = Percentage(dashboard.SecondaryProviders, dashboard.TotalProviders), DecisionSignal = "Referral and escalation capacity" },
                new() { Area = "Encounter", Indicator = "Service utilization", Count = dashboard.TotalEncounters, Rate = dashboard.ServiceUtilizationRate, DecisionSignal = $"{dashboard.UniqueServiceUsers:N0} unique users accessed care" },
                new() { Area = "Claims", Indicator = "Paid claim rate", Count = dashboard.TotalClaims, Rate = dashboard.ClaimApprovalRate, DecisionSignal = $"NGN {dashboard.TotalClaimValue:N2} submitted claim value" },
                new() { Area = "Complaints", Indicator = "Complaint resolution", Count = dashboard.ComplaintMetrics.TotalComplaints, Rate = dashboard.ComplaintMetrics.ResolutionRate, DecisionSignal = $"{dashboard.ComplaintMetrics.CriticalComplaints:N0} critical open complaints" },
                new() { Area = "Referral", Indicator = "Referral completion", Count = dashboard.TotalReferrals, Rate = dashboard.ReferralCompletionRate, DecisionSignal = $"{dashboard.PendingReferrals:N0} pending referrals" },
                new() { Area = "Capitation", Indicator = "Paid capitation", Count = dashboard.Capitation.TotalPayments, Rate = Percentage(dashboard.Capitation.PaidPayments, dashboard.Capitation.TotalPayments), DecisionSignal = $"NGN {dashboard.Capitation.PaidAmount:N2} paid" },
                new() { Area = "Users", Indicator = "Active users last 30 days", Count = dashboard.TotalUsers, Rate = Percentage(dashboard.ActiveUsersLast30Days, dashboard.TotalUsers), DecisionSignal = "Operational user activity" },
                new() { Area = "Reports", Indicator = "Audited report rate", Count = dashboard.TotalReports, Rate = Percentage(dashboard.AuditedReports, dashboard.TotalReports), DecisionSignal = $"{dashboard.PendingReports:N0} reports pending audit" },
                new() { Area = "Vulnerable Groups", Indicator = "Vulnerable population share", Count = dashboard.VulnerableEnrolleeCount, Rate = dashboard.VulnerablePopulationPercentage, DecisionSignal = "Equity monitoring" },
                new() { Area = "Gender", Indicator = "Female enrollee share", Count = dashboard.FemaleCount, Rate = dashboard.FemalePercentage, DecisionSignal = $"Male share {dashboard.MalePercentage:N1}%" },
                new() { Area = "Disease Trend", Indicator = "Recorded diagnosis trend", Count = dashboard.DiseaseTrends.Sum(x => x.Encounters), Rate = dashboard.DiseaseTrends.FirstOrDefault()?.Percentage ?? 0m, DecisionSignal = dashboard.DiseaseTrends.FirstOrDefault()?.Diagnosis ?? "No diagnosis trend yet" }
            };
        }
        private async Task<List<StateMonitoringViewModel>> BuildStateIndicatorsAsync(
            string scope,
            string selectedLga,
            int? hmoId,
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

            if (hmoId.HasValue)
            {
                int hmoFilter = hmoId.Value;
                enrollees = enrollees.Where(x => x.HmoId == hmoFilter);
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

                if (hmoId.HasValue)
                {
                    int hmoFilter = hmoId.Value;
                    providerQuery = providerQuery.Where(x => x.HmoId == hmoFilter);
                    encounterQuery = encounterQuery.Where(x => x.Enrollee != null && x.Enrollee.HmoId == hmoFilter);
                    claimQuery = claimQuery.Where(x => x.HmoId == hmoFilter || (x.Enrollee != null && x.Enrollee.HmoId == hmoFilter));
                    complaintQuery = complaintQuery.Where(x => x.HmoId == hmoFilter);
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

        private static string BuildScopeDisplay(string scope, string selectedLga, string? selectedHmoName)
        {
            string location = scope == CtsTargetScope
                ? CtsTargetScope
                : string.IsNullOrWhiteSpace(selectedLga)
                    ? scope
                    : $"{scope} / {selectedLga}";

            return string.IsNullOrWhiteSpace(selectedHmoName)
                ? location
                : $"{location} / {selectedHmoName}";
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






