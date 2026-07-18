// Models/Encounter.cs
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

        [Display(Name = "Attending Doctor")]
        public int? DoctorId { get; set; }
        public Doctor? Doctor { get; set; }

        public DateTime VisitDate { get; set; } = DateTime.Now;

        [Required]
        public string ChiefComplaint { get; set; } = string.Empty;

        public string? Diagnosis { get; set; }
        public string? LabTests { get; set; }
        public string? TreatmentGiven { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal ConsultationFee { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal LabFee { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal DrugFee { get; set; }
        public decimal TotalAmount => ConsultationFee + LabFee + DrugFee;
        public int? ClaimId { get; set; }  // Links to Claim (nullable until claimed)
        public Claim? Claim { get; set; }

        public string Status { get; set; } = "Completed"; // Completed, Billed, Claimed
        public decimal Temperature { get; set; }
        public string? BloodPressure { get; set; }
        public string? VisitType { get; set; }
        [Required]
        public string ServiceSetting { get; set; } = EncounterServiceCatalog.Outpatient;

        public ICollection<EncounterService> Services { get; set; } = new List<EncounterService>();

        [NotMapped]
        public List<string> SelectedServices { get; set; } = new();

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
