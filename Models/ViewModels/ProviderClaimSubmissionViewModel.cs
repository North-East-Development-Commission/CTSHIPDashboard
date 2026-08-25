using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Models;
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

    public string CatalogState { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    [Display(Name = "Diagnosis")]
    public List<string> SelectedDiagnoses { get; set; } = new();

    [StringLength(200)]
    [Display(Name = "Other diagnosis")]
    public string? DiagnosisOther { get; set; }

    public string Treatment { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Service Category")]
    public string ServiceCategory { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Other service category")]
    public string? OtherServiceCategory { get; set; }

    [StringLength(1000)]
    [Display(Name = "Specific Service / Procedure Provided")]
    public string ServiceProcedure { get; set; } = string.Empty;

    [Display(Name = "Prescription")]
    public List<string> SelectedPrescriptions { get; set; } = new();

    [Display(Name = "Laboratory Investigations")]
    public List<string> SelectedLaboratoryTests { get; set; } = new();

    [Display(Name = "Surgery")]
    public List<string> SelectedSurgeries { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> PrescriptionCatalog { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> LaboratoryCatalog { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> SurgeryCatalog { get; set; } = new();

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
