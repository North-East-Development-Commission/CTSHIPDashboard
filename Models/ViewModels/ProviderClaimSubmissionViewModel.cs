using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CTSHIPDashboard.ViewModels;

public class ProviderClaimSubmissionViewModel
{
    public int EncounterId { get; set; }

    public string EncounterNumber { get; set; } = string.Empty;

    public DateTime VisitDate { get; set; }

    public string EnrolleeName { get; set; } = string.Empty;

    public string EnrollmentNumber { get; set; } = string.Empty;

    public string HmoName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderLevel { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public string Treatment { get; set; } = string.Empty;

    [Display(Name = "Claim Evidence")]
    public List<IFormFile> EvidenceFiles { get; set; } = new();
}
