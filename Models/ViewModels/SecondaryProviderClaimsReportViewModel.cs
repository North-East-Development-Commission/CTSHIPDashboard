namespace CTSHIPDashboard.Models.ViewModels;

public class SecondaryProviderClaimsReportViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalClaims { get; set; }
    public int SubmittedClaims { get; set; }
    public int QueryClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int PaidClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int CertifiedClaims { get; set; }
    public int IhsaVerifiedClaims { get; set; }
    public decimal TotalClaimAmount { get; set; }
    public decimal PaidClaimAmount { get; set; }
    public List<SecondaryProviderClaimRowViewModel> Claims { get; set; } = new();
    public List<SecondaryProviderClaimProviderSummaryViewModel> ProviderSummaries { get; set; } = new();
    public List<SecondaryProviderClaimStatusSummaryViewModel> StatusSummaries { get; set; } = new();
}

public class SecondaryProviderClaimRowViewModel
{
    public int Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string EnrolleeName { get; set; } = string.Empty;
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string HmoName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HmoCertificationStatus { get; set; } = string.Empty;
    public string IhsaVerificationStatus { get; set; } = string.Empty;
    public int OpenQueries { get; set; }
    public DateTime DateSubmitted { get; set; }
}

public class SecondaryProviderClaimProviderSummaryViewModel
{
    public string ProviderName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Claims { get; set; }
    public decimal Amount { get; set; }
    public int QueryClaims { get; set; }
    public int PaidClaims { get; set; }
}

public class SecondaryProviderClaimStatusSummaryViewModel
{
    public string Status { get; set; } = string.Empty;
    public int Claims { get; set; }
    public decimal Amount { get; set; }
}
