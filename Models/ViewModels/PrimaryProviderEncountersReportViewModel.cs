namespace CTSHIPDashboard.Models.ViewModels;

public class PrimaryProviderEncountersReportViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalEncounters { get; set; }
    public int QueryEncounters { get; set; }
    public int HmoCertifiedEncounters { get; set; }
    public int IhsaVerifiedEncounters { get; set; }
    public int UniqueEnrollees { get; set; }
    public decimal TotalCapitationCharge { get; set; }
    public List<PrimaryProviderEncounterRowViewModel> Encounters { get; set; } = new();
    public List<PrimaryProviderEncounterProviderSummaryViewModel> ProviderSummaries { get; set; } = new();
    public List<PrimaryProviderEncounterServiceSummaryViewModel> ServiceSummaries { get; set; } = new();
}

public class PrimaryProviderEncounterRowViewModel
{
    public int Id { get; set; }
    public string EncounterNumber { get; set; } = string.Empty;
    public string EnrolleeName { get; set; } = string.Empty;
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string HmoName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string ReasonForEncounter { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public decimal CapitationCharge { get; set; }
    public string HmoVerificationStatus { get; set; } = string.Empty;
    public string IhsaVerificationStatus { get; set; } = string.Empty;
    public int OpenQueries { get; set; }
}

public class PrimaryProviderEncounterProviderSummaryViewModel
{
    public string ProviderName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Encounters { get; set; }
    public int UniqueEnrollees { get; set; }
    public decimal CapitationCharge { get; set; }
    public int QueryEncounters { get; set; }
}

public class PrimaryProviderEncounterServiceSummaryViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public int Encounters { get; set; }
}
