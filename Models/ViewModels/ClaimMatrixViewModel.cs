namespace CTSHIPDashboard.Models.ViewModels;

public class ClaimMatrixViewModel
{
    public int TotalClaims { get; set; }
    public int SubmittedClaims { get; set; }
    public int ClaimsValidated { get; set; }
    public int QueryClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int PaidClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int OutstandingClaims { get; set; }
    public decimal TotalClaimAmount { get; set; }
    public decimal ApprovedClaimAmount { get; set; }
    public decimal PaidClaimAmount { get; set; }
    public decimal OutstandingClaimAmount { get; set; }
    public double AverageProcessingDays { get; set; }
}
