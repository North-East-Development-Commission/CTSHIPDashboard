// Models/MedicalHistory.cs
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class MedicalHistory
    {
        public int Id { get; set; }

        [Required]
        public int EnrolleeId { get; set; }
        public Enrollee? Enrollee { get; set; }

        [Required]
        public DateTime DateRecorded { get; set; } = DateTime.Now;

        public string Condition { get; set; } = string.Empty;           // e.g. Hypertension
        public string DiagnosisDate { get; set; } = string.Empty;       // e.g. 2023
        public string Status { get; set; } = "Active";                  // Active, Controlled, Resolved
        public string Medication { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;
        public string Surgeries { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public string RecordedBy { get; set; } = string.Empty;          // Provider/HMO name
    }
}