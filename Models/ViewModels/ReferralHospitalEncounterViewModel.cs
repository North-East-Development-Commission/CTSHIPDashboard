using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.ViewModels;

public class ReferralHospitalEncounterViewModel
{
    public Guid ReferralId { get; set; }

    public string EnrolleeNumber { get; set; } = string.Empty;

    public string EnrolleeFullName { get; set; } = string.Empty;

    public string FromProviderName { get; set; } = string.Empty;

    public string ReferredHospitalName { get; set; } = string.Empty;

    public string? HmoName { get; set; }

    public string CatalogState { get; set; } = string.Empty;

    public string DiagnosisFromReferral { get; set; } = string.Empty;

    public string ReasonForReferral { get; set; } = string.Empty;

    [Display(Name = "Visit Date & Time")]
    public DateTime VisitDate { get; set; } = DateTime.Now;

    [Required]
    [Display(Name = "Visit Type")]
    public string VisitType { get; set; } = "Referral";

    [Required]
    [Display(Name = "Service Setting")]
    public string ServiceSetting { get; set; } = EncounterServiceCatalog.Outpatient;

    [Display(Name = "Services Delivered")]
    public List<string> SelectedServices { get; set; } = new();

    [Display(Name = "Prescription")]
    public List<string> SelectedPrescriptions { get; set; } = new();

    [Display(Name = "Laboratory Tests")]
    public List<string> SelectedLaboratoryTests { get; set; } = new();

    [Display(Name = "Surgery")]
    public List<string> SelectedSurgeries { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> PrescriptionCatalog { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> LaboratoryCatalog { get; set; } = new();

    public List<ReferralEncounterClaimCatalogItem> SurgeryCatalog { get; set; } = new();

    public bool HasAnyPriceCatalog =>
        PrescriptionCatalog.Count > 0 || LaboratoryCatalog.Count > 0 || SurgeryCatalog.Count > 0;

    [Required]
    [Display(Name = "Complaint")]
    public string ChiefComplaint { get; set; } = string.Empty;

    [Required]
    public string Diagnosis { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Treatment Given")]
    public string TreatmentGiven { get; set; } = string.Empty;

    [Display(Name = "Temperature (C)")]
    public decimal Temperature { get; set; } = 36.5m;

    [Display(Name = "Blood Pressure")]
    public string BloodPressure { get; set; } = "120/80";

    [Display(Name = "Pulse Rate")]
    public int PulseRate { get; set; } = 72;

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Consultation Fee")]
    public decimal ConsultationFee { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Lab Fee")]
    public decimal LabFee { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Drug Fee")]
    public decimal DrugFee { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Surgery Fee")]
    public decimal SurgeryFee { get; set; }

    [Display(Name = "Fees Waived")]
    public bool FeesWaived { get; set; } = true;

    public string? Notes { get; set; }

    [Display(Name = "Evidence of Findings")]
    public IFormFile? FindingsEvidenceFile { get; set; }

    [Display(Name = "Other Claim Supporting Documents")]
    public List<IFormFile> SupportingDocumentFiles { get; set; } = new();

    public decimal TotalAmount => ConsultationFee + LabFee + DrugFee + SurgeryFee;

    public List<SelectListItem> ServiceSettings { get; set; } = new();

    public List<SelectListItem> VisitTypes { get; set; } = new();
}
