// Models/Encounter.cs
using CTSHIPDashboard.Models;
using System.ComponentModel.DataAnnotations;

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

        public DateTime VisitDate { get; set; } = DateTime.Now;

        [Required]
        public string ChiefComplaint { get; set; } = string.Empty;

        public string? Diagnosis { get; set; }
        public string? LabTests { get; set; }
        public string? TreatmentGiven { get; set; }

        public decimal ConsultationFee { get; set; } 
        public decimal LabFee { get; set; } 
        public decimal DrugFee { get; set; } 
        public decimal TotalAmount => ConsultationFee + LabFee + DrugFee;

        public int? ClaimId { get; set; }  // Links to Claim (nullable until claimed)
        public Claim? Claim { get; set; }

        public string Status { get; set; } = "Completed"; // Completed, Billed, Claimed
        public decimal Temperature { get; set; }
        public string? BloodPressure { get; set; }
        public string? VisitType { get; set; }
        public int PulseRate { get; set; }
        public string? Notes { get; set; }
        public string? AttendedBy { get; set; }
        public string? SeenBy { get; set; }
        public string? Rank { get; set; }

        public string EncounterNumber { get; set; } = string.Empty;
        public bool IsBilled { get; set; }
    }
}