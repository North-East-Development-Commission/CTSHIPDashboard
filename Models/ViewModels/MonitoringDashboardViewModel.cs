using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class MonitoringDashboardViewModel
    {
        public string Scope { get; set; } = "CTSHIP";
        public string ScopeDisplay { get; set; } = "CTSHIP";
        public string SelectedState { get; set; } = string.Empty;
        public string SelectedLga { get; set; } = string.Empty;
        public int? SelectedHmoId { get; set; }
        public List<string> AvailableStates { get; set; } = new();
        public List<string> AvailableLgas { get; set; } = new();

        public int TargetEnrollees { get; set; }
        public int TotalEnrolled { get; set; }
        public int ActiveEnrollees { get; set; }
        public decimal CoveragePercentage { get; set; }
        public decimal ActiveEnrolleeRate { get; set; }

        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherGenderCount { get; set; }
        public decimal MalePercentage { get; set; }
        public decimal FemalePercentage { get; set; }

        public int PregnantWomenCount { get; set; }
        public int UnderFiveCount { get; set; }
        public int PlwdCount { get; set; }
        public int ElderlyCount { get; set; }
        public int OtherVulnerableCount { get; set; }
        public int VulnerableEnrolleeCount { get; set; }

        public decimal PregnantWomenPercentage { get; set; }
        public decimal UnderFivePercentage { get; set; }
        public decimal PlwdPercentage { get; set; }
        public decimal ElderlyPercentage { get; set; }
        public decimal OtherVulnerablePercentage { get; set; }
        public decimal VulnerablePopulationPercentage { get; set; }

        public List<MonitoringCategoryViewModel> VulnerableDistribution { get; set; } = new();
        public decimal VulnerableDistributionTotal { get; set; }

        public int ImmunizationEncounters { get; set; }
        public int AncEncounters { get; set; }
        public int FamilyPlanningEncounters { get; set; }
        public int HealthPromotionEncounters { get; set; }
        public int OtherPreventiveEncounters { get; set; }
        public int TotalPreventiveEncounters { get; set; }
        public decimal PreventiveCareRatePerThousand { get; set; }
        public int UniqueServiceUsers { get; set; }
        public int TotalEncounterServices { get; set; }
        public decimal ServiceUtilizationRate { get; set; }
        public List<ServiceFrequencyViewModel> MostUsedServices { get; set; } = new();

        public int TotalProviders { get; set; }
        public int PrimaryProviders { get; set; }
        public int SecondaryProviders { get; set; }
        public int ReferralProviders { get; set; }
        public int TotalHmos { get; set; }
        public int TotalEncounters { get; set; }
        public int TotalVisits { get; set; }
        public decimal EncounterRatePerThousand { get; set; }
        public int TotalClaims { get; set; }
        public int PaidClaims { get; set; }
        public int PendingClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal ClaimApprovalRate { get; set; }
        public decimal TotalClaimValue { get; set; }
        public decimal PaidClaimValue { get; set; }
        public int AuditedDeaths { get; set; }
        public decimal DeathRatePerThousand { get; set; }
        public int TotalReferrals { get; set; }
        public int CompletedReferrals { get; set; }
        public int PendingReferrals { get; set; }
        public int RejectedReferrals { get; set; }
        public decimal ReferralCompletionRate { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsersLast30Days { get; set; }
        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int AuditedReports { get; set; }
        public int ReportsNeedingCorrection { get; set; }
        public CapitationSummaryViewModel Capitation { get; set; } = new();
        public ComplaintMetricsViewModel ComplaintMetrics { get; set; } = new();
        public List<SystemActivityMatrixRowViewModel> SystemActivityMatrix { get; set; } = new();
        public List<HmoOversightRowViewModel> HmoOversight { get; set; } = new();
        public List<ProviderLevelMetricViewModel> ProviderLevelMetrics { get; set; } = new();
        public List<DiseaseTrendViewModel> DiseaseTrends { get; set; } = new();
        public List<StateMonitoringViewModel> StateIndicators { get; set; } = new();
        public EncounterDemographicMatrixViewModel EncounterDemographicMatrix { get; set; } = new();
    }

    public class MonitoringCategoryViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ServiceFrequencyViewModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceSetting { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public decimal PercentageOfRecordedServices { get; set; }
    }

    public class StateMonitoringViewModel
    {
        public string State { get; set; } = string.Empty;
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int Providers { get; set; }
        public int Encounters { get; set; }
        public int Claims { get; set; }
        public int PaidClaims { get; set; }
        public decimal ClaimValue { get; set; }
        public decimal PaidClaimValue { get; set; }
        public int Referrals { get; set; }
        public int CompletedReferrals { get; set; }
        public decimal ReferralCompletionRate { get; set; }
        public int Complaints { get; set; }
        public int OpenComplaints { get; set; }
    }
    public class SystemActivityMatrixRowViewModel
    {
        public string Area { get; set; } = string.Empty;
        public string Indicator { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Rate { get; set; }
        public string DecisionSignal { get; set; } = string.Empty;
    }

    public class HmoOversightRowViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string States { get; set; } = string.Empty;
        public int Enrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int Providers { get; set; }
        public int Encounters { get; set; }
        public int Claims { get; set; }
        public decimal ClaimValue { get; set; }
        public int Complaints { get; set; }
        public decimal ServiceUtilizationRate { get; set; }
    }

    public class ProviderLevelMetricViewModel
    {
        public string Level { get; set; } = string.Empty;
        public int Providers { get; set; }
        public int Enrollees { get; set; }
        public int Encounters { get; set; }
        public decimal EncounterRatePerThousand { get; set; }
    }

    public class DiseaseTrendViewModel
    {
        public string Diagnosis { get; set; } = string.Empty;
        public int Encounters { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CapitationSummaryViewModel
    {
        public int TotalPayments { get; set; }
        public int PaidPayments { get; set; }
        public int PendingPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AverageUtilizationRate { get; set; }
    }
    public class MonitoringTargetViewModel
    {
        [Required]
        public string Scope { get; set; } = "CTSHIP";

        public string? Lga { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Target enrollees must be greater than zero.")]
        [Display(Name = "Target Enrollees")]
        public int TargetEnrollees { get; set; }
    }
}








