namespace CTSHIPDashboard.Models.ViewModels;

public class SecondaryProviderClaimsReportViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalClaims { get; set; }
    public int ClaimsValidated { get; set; }
    public int SubmittedClaims { get; set; }
    public int QueryClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int PartiallyApprovedClaims { get; set; }
    public int PaidClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int CertifiedClaims { get; set; }
    public int IhsaVerifiedClaims { get; set; }
    public decimal TotalClaimAmount { get; set; }
    public decimal ApprovedClaimAmount { get; set; }
    public decimal PaidClaimAmount { get; set; }
    public decimal OutstandingClaimAmount { get; set; }
    public double AverageProcessingDays { get; set; }
    public List<SecondaryProviderClaimRowViewModel> Claims { get; set; } = new();
    public List<SecondaryProviderClaimProviderSummaryViewModel> ProviderSummaries { get; set; } = new();
    public List<SecondaryProviderClaimStatusSummaryViewModel> StatusSummaries { get; set; } = new();
}

public class SecondaryProviderClaimRowViewModel
{
    public int Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string ReportingMonth { get; set; } = string.Empty;
    public string EnrolleeName { get; set; } = string.Empty;
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string HmoName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? DateOfService { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty;
    public string ReferralFacility { get; set; } = string.Empty;
    public string AuthorizationNumber { get; set; } = string.Empty;
    public string ServiceProcedure { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public decimal ApprovedTariff { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountApproved { get; set; }
    public decimal DeductionAmount { get; set; }
    public string DeductionReason { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public string AdjustmentReason { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string HmoCertificationStatus { get; set; } = string.Empty;
    public string IhsaVerificationStatus { get; set; } = string.Empty;
    public int OpenQueries { get; set; }
    public int ProcessingDays { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public DateTime DateSubmitted { get; set; }
}

public class SecondaryProviderClaimProviderSummaryViewModel
{
    public string ProviderName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Claims { get; set; }
    public decimal Amount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int QueryClaims { get; set; }
    public int PaidClaims { get; set; }
}

public class SecondaryProviderClaimStatusSummaryViewModel
{
    public string Status { get; set; } = string.Empty;
    public int Claims { get; set; }
    public decimal Amount { get; set; }
}