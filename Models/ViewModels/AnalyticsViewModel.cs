public class AnalyticsViewModel
{
    public int TotalClaims { get; set; }
    public int SubmittedClaims { get; set; }
    public int ClaimsValidated { get; set; }
    public int QueryClaims { get; set; }
    public int PaidClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int OutstandingClaims { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal RejectedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public double ApprovalRate { get; set; }
    public double AverageProcessingDays { get; set; }

    public List<ChartData> ClaimsByState { get; set; } = new();
    public List<ChartData> ClaimsByMonth { get; set; } = new();
    public List<ChartData> TopDiagnoses { get; set; } = new();
}

public class ChartData
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}