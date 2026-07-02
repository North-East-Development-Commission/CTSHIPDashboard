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

        public int TotalActiveEnrollees { get; set; }

        public int TotalVisits { get; set; }

        public int TotalEncounters { get; set; }

        public int EnrolleesAccessingCare { get; set; }

        public int ServiceUtilization { get; set; }

        public int TotalReferrals { get; set; }

        public int CompletedReferrals { get; set; }

        public decimal ReferralCompletionRate { get; set; }

        public decimal AmountCapitationPaid { get; set; }

        public decimal CapitationToUtilizationRatio { get; set; }

        public int TotalClaims { get; set; }

        public decimal TotalClaimsAmount { get; set; }

        public int PaidClaims { get; set; }

        public decimal PaidClaimsAmount { get; set; }

        [Required, StringLength(50)]
        public string AuditStatus { get; set; } = "Pending";

        [StringLength(450)]
        public string? AuditedByUserId { get; set; }

        [StringLength(200)]
        public string? AuditedByName { get; set; }

        public DateTime? AuditedAt { get; set; }

        [StringLength(1000)]
        public string? AuditNote { get; set; }
    }
}
