using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.ViewModels;

public class EncounterReferralInputViewModel
{
    [Display(Name = "Refer Patient")]
    public bool RequiresReferral { get; set; }

    [Display(Name = "Referred Hospital")]
    public Guid? ReferredHospitalId { get; set; }

    [StringLength(200)]
    public string? Diagnosis { get; set; }

    [StringLength(1000)]
    [Display(Name = "Reason For Referral")]
    public string? ReasonForReferral { get; set; }

    [StringLength(1000)]
    [Display(Name = "Clinical Summary")]
    public string? ClinicalSummary { get; set; }

    [StringLength(1000)]
    [Display(Name = "Treatment Given")]
    public string? TreatmentGiven { get; set; }

    [StringLength(1000)]
    [Display(Name = "Investigation Summary")]
    public string? InvestigationSummary { get; set; }

    public ReferralPriority Priority { get; set; } = ReferralPriority.Routine;

    public List<SelectListItem> ReferredHospitals { get; set; } = new();
}
