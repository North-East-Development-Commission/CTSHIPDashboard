using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class StateOfficeDashboardViewModel
    {
        public string StateName { get; set; } = string.Empty;
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int TotalClaims { get; set; }
        public int SubmittedClaims { get; set; }
        public int ClaimsValidated { get; set; }
        public int QueryClaims { get; set; }
        public int PaidClaims { get; set; }
        public int RejectedClaims { get; set; }
        public int OutstandingClaims { get; set; }
        public decimal TotalClaimValue { get; set; }
        public decimal ApprovedClaimValue { get; set; }
        public decimal PaidClaimValue { get; set; }
        public decimal OutstandingClaimValue { get; set; }
        public double AverageProcessingDays { get; set; }
        public int HmoCount { get; set; }
        public List<EnrolleeSummaryViewModel> RecentEnrollees { get; set; } = new();
        public int TotalProviders { get; internal set; }
        public MonitoringDashboardViewModel Monitoring { get; set; } = new();
        public ComplaintMetricsViewModel ComplaintMetrics { get; set; } = new();
    }

    public class EnrolleeSummaryViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EnrollmentNumber { get; set; } = string.Empty;
        public string HmoName { get; set; } = string.Empty;
        public DateTime DateRegistered { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class StateOfficeClaimsViewModel
    {
        public string StateName { get; set; } = string.Empty;
        public string Search { get; set; } = string.Empty;
        public string Status { get; set; } = "All";
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalFilteredClaims { get; set; }
        public int TotalClaims { get; set; }
        public int SubmittedClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ClaimsValidated { get; set; }
        public int QueryClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int PaidClaims { get; set; }
        public int RejectedClaims { get; set; }
        public int OutstandingClaims { get; set; }
        public decimal TotalClaimValue { get; set; }
        public decimal ApprovedClaimValue { get; set; }
        public decimal PendingClaimValue { get; set; }
        public decimal PaidClaimValue { get; set; }
        public decimal OutstandingClaimValue { get; set; }
        public double AverageProcessingDays { get; set; }
        public List<string> AvailableStates { get; set; } = new();
        public List<StateOfficeClaimRowViewModel> Claims { get; set; } = new();
    }

    public class StateOfficeClaimRowViewModel
    {
        public int Id { get; set; }
        public string ClaimNumber { get; set; } = string.Empty;
        public string EnrolleeName { get; set; } = string.Empty;
        public string EnrollmentNumber { get; set; } = string.Empty;
        public string HmoName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateSubmitted { get; set; }
        public DateTime? DatePaid { get; set; }
    }

    public class StateOfficeClaimDetailsViewModel
    {
        public string StateName { get; set; } = string.Empty;
        public Claim Claim { get; set; } = new();
    }
}
