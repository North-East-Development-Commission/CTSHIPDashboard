using CTSHIPDashboard.ViewModels;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class NHIADashboardViewModel
    {
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int NewEnrollmentsThisMonth { get; set; }
        public decimal ActiveEnrolleeRate { get; set; }

        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int PaidClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal ClaimPaymentRate { get; set; }
        public decimal PaidClaimAmount { get; set; }

        public int TotalHMOs { get; set; }
        public int ActiveHMOs { get; set; }
        public int HmosWithProviders { get; set; }

        public int TotalProviders { get; set; }
        public int ActiveProviders { get; set; }
        public int ProvidersWithEncounters { get; set; }
        public decimal ProviderActivityRate { get; set; }

        public int TotalEncounters { get; set; }
        public int UniqueServiceUsers { get; set; }
        public int EncounterServicesRecorded { get; set; }
        public decimal ServiceUtilizationRate { get; set; }
        public decimal EncounterRatePerThousand { get; set; }

        public decimal TotalClaimAmount { get; set; }
        public ComplaintMetricsViewModel ComplaintMetrics { get; set; } = new();

        public List<StateSummary> StateSummaries { get; set; } = new();
        public List<RecentEnrollee> RecentEnrollees { get; set; } = new();
        public List<RecentClaimSummary> RecentClaims { get; set; } = new();
        public List<ServiceFrequencyViewModel> TopServices { get; set; } = new();
        public List<ProgramOversightSignal> OversightSignals { get; set; } = new();
        public EncounterDemographicMatrixViewModel EncounterDemographicMatrix { get; set; } = new();
    }

    public class StateSummary
    {
        public string StateName { get; set; } = string.Empty;
        public int Enrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int Claims { get; set; }
        public int Providers { get; set; }
        public int Hmos { get; set; }
        public int Encounters { get; set; }
        public int Complaints { get; set; }
        public decimal ClaimAmount { get; set; }
        public decimal UtilizationRate { get; set; }
    }

    public class RecentClaimSummary
    {
        public string ClaimNumber { get; set; } = string.Empty;
        public string EnrolleeName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateSubmitted { get; set; }
    }

    public class ProgramOversightSignal
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string IconCss { get; set; } = "bi-activity";
        public string ToneCss { get; set; } = "text-success";
    }
}
