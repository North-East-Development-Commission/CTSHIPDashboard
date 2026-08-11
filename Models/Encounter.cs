// Models/Encounter.cs
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    public class Encounter
    {
        public int Id { get; set; }

        [Required]
        public int EnrolleeId { get; set; }
        public Enrollee? Enrollee { get; set; }

        [Required]
        public int ProviderId { get; set; }
        public Provider? Provider { get; set; }

        [Display(Name = "Attended By (Hospital Staff)")]
        public int? DoctorId { get; set; }
        public Doctor? Doctor { get; set; }

        public DateTime VisitDate { get; set; } = DateTime.Now;

        [Required]
        public string ChiefComplaint { get; set; } = string.Empty;

        public string? Diagnosis { get; set; }
        public string? LabTests { get; set; }
        public string? TreatmentGiven { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Reason For Encounter")]
        public string ReasonForEncounter { get; set; } = "Acute illness";

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal ConsultationFee { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal LabFee { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal DrugFee { get; set; }

        [NotMapped]
        public decimal ServiceTotal => ConsultationFee + LabFee + DrugFee;

        [NotMapped]
        public decimal TotalAmount => ServiceTotal + CapitationCharge;

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal CapitationCharge { get; set; }

        [StringLength(50)]
        public string HmoVerificationStatus { get; set; } = "Submitted";

        [StringLength(200)]
        public string? HmoVerifiedBy { get; set; }

        public DateTime? HmoVerifiedAt { get; set; }

        [StringLength(1000)]
        public string? HmoVerificationNote { get; set; }

        [StringLength(50)]
        public string IhsaVerificationStatus { get; set; } = "Not Ready";

        [StringLength(200)]
        public string? IhsaVerifiedBy { get; set; }

        public DateTime? IhsaVerifiedAt { get; set; }

        [StringLength(1000)]
        public string? IhsaVerificationNote { get; set; }

        public DateTime? SubmittedToHmoAt { get; set; }
        public DateTime? ReturnedForClarificationAt { get; set; }

        [StringLength(200)]
        public string? ReturnedForClarificationBy { get; set; }

        [StringLength(1000)]
        public string? ClarificationNote { get; set; }

        public string? OriginalFacilityDataJson { get; set; }

        public ICollection<EncounterQuery> Queries { get; set; } = new List<EncounterQuery>();
        public ICollection<EncounterAuditTrail> AuditTrails { get; set; } = new List<EncounterAuditTrail>();

        public int? ClaimId { get; set; }
        public Claim? Claim { get; set; }

        public string Status { get; set; } = "Completed";
        public decimal Temperature { get; set; }
        public string? BloodPressure { get; set; }
        public string? VisitType { get; set; }

        [Required]
        public string ServiceSetting { get; set; } = EncounterServiceCatalog.Outpatient;

        public ICollection<EncounterService> Services { get; set; } = new List<EncounterService>();
        public ICollection<EncounterPrescription> Prescriptions { get; set; } = new List<EncounterPrescription>();

        // Persisted presenting complaints (normalized)
        public ICollection<EncounterPresentingComplaint> PresentingComplaints { get; set; } = new List<EncounterPresentingComplaint>();

        [NotMapped]
        public List<string> SelectedServices { get; set; } = new();

        [NotMapped]
        public List<string> SelectedPresentingComplaints { get; set; } = new();

        [NotMapped]
        public string? PresentingComplaintsOther { get; set; }

        [NotMapped]
        public List<EncounterPrescriptionInputViewModel> SelectedPrescriptions { get; set; } = new();

        [NotMapped]
        public List<string> SelectedLaboratoryTests { get; set; } = new();

        [NotMapped]
        public List<CTSHIPDashboard.ViewModels.EncounterLaboratoryInputViewModel> LaboratoryInvestigations { get; set; } = new();

        [NotMapped]
        public bool FeesWaived { get; set; }

        [NotMapped]
        public EncounterReferralInputViewModel Referral { get; set; } = new();

        public int PulseRate { get; set; }
        public string? Notes { get; set; }
        public string? AttendedBy { get; set; }
        public string? SeenBy { get; set; }
        public string? Rank { get; set; }

        public string EncounterNumber { get; set; } = string.Empty;
        public bool IsBilled { get; set; }
    }
}