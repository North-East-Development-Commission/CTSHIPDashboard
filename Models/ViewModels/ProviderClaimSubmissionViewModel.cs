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

    [Required]
    [StringLength(150)]
    [Display(Name = "Diagnosis / Service Category")]
    public string ServiceCategory { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Display(Name = "Specific Service / Procedure Provided")]
    public string ServiceProcedure { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Referral Facility")]
    public string? ReferralFacility { get; set; }

    [StringLength(100)]
    [Display(Name = "Authorization Number")]
    public string? AuthorizationNumber { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Approved Tariff")]
    public decimal ApprovedTariff { get; set; }

    [Display(Name = "Claim Evidence")]
    public List<IFormFile> EvidenceFiles { get; set; } = new();
}