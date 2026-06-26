using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.ViewModels;

public class ReferralCreateViewModel
{
    public Guid? EncounterId { get; set; }

    [StringLength(100)]
    [Display(Name = "Encounter Reference")]
    public string? EncounterReference { get; set; }

    public Guid? EnrolleeId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Enrollee Number")]
    public string EnrolleeNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Enrollee Full Name")]
    public string EnrolleeFullName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "HMO Code")]
    public string? HmoCode { get; set; }

    [StringLength(200)]
    [Display(Name = "HMO Name")]
    public string? HmoName { get; set; }

    [StringLength(100)]
    [Display(Name = "From Provider ID")]
    public string? FromProviderId { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "From Provider")]
    public string FromProviderName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Referred Hospital")]
    public Guid? ReferredHospitalId { get; set; }

    [Required]
    [StringLength(200)]
    public string Diagnosis { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    [Display(Name = "Reason For Referral")]
    public string ReasonForReferral { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Clinical Summary")]
    public string? ClinicalSummary { get; set; }

    [StringLength(1000)]
    [Display(Name = "Treatment Given")]
    public string? TreatmentGiven { get; set; }

    [StringLength(1000)]
    [Display(Name = "Investigation Summary")]
    public string? InvestigationSummary { get; set; }

    [Display(Name = "Priority")]
    public ReferralPriority Priority { get; set; } = ReferralPriority.Routine;

    public List<SelectListItem> ReferredHospitals { get; set; } = new();
}
