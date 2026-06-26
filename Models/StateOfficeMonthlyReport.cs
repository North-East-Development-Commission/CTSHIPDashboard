using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class StateOfficeMonthlyReport
    {
        public int Id { get; set; }

        public DateTime ReportingMonth { get; set; }

        [Required, StringLength(100)]
        public string State { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Lga { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Ward { get; set; } = string.Empty;

        public int ProviderId { get; set; }

        [Required, StringLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string FacilityCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string ReportingOfficerName { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Designation { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; }

        [StringLength(450)]
        public string? SubmittedByUserId { get; set; }

        [StringLength(200)]
        public string? SubmittedByName { get; set; }
    }
}
