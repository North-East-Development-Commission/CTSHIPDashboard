// Models/Claim.cs
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class Claim
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ClaimNumber { get; set; } = string.Empty;

        [Required]
        public int? EnrolleeId { get; set; }
        public Enrollee? Enrollee { get; set; }
        public int? HmoId { get; set; }
        public Hmo? Hmos { get; set; }

        [Required]
        public int ProviderId { get; set; }
        public Provider? Provider { get; set; }

        public int? EncounterId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public string Diagnosis { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;

        public DateTime? DateOfService { get; set; }
        public string? ServiceCategory { get; set; }
        public string? ReferralFacility { get; set; }
        public string? AuthorizationNumber { get; set; }
        public string? ServiceProcedure { get; set; }
        public decimal ApprovedTariff { get; set; }
        public decimal AmountApproved { get; set; }
        public decimal DeductionAmount { get; set; }
        public string? DeductionReason { get; set; }
        public decimal AmountPaid { get; set; }

        public DateTime DateSubmitted { get; set; } = DateTime.Now;
        public DateTime? DateProcessed { get; set; }

        [Required]
        public string Status { get; set; } = "Submitted";

        public string SubmittedBy { get; set; } = string.Empty;

        public string? ReviewedBy { get; set; }
        public DateTime? DateReviewed { get; set; }
        public string? ReviewNotes { get; set; }

        public string? ApprovedBy { get; set; }
        public DateTime? DateApproved { get; set; }
        public string? ApprovalNotes { get; set; }

        public string? PaidBy { get; set; }
        public DateTime? DatePaid { get; set; }
        public string? PaymentReference { get; set; }

        public string? RejectionReason { get; set; }
        public string? RejectedBy { get; set; }
        public DateTime? DateRejected { get; set; }

        public DateTime? ReturnedForClarificationAt { get; set; }
        public string? ReturnedForClarificationBy { get; set; }
        public string? ClarificationNote { get; set; }

        public string HmoCertificationStatus { get; set; } = "Not Certified";
        public string? HmoCertifiedBy { get; set; }
        public DateTime? HmoCertifiedAt { get; set; }
        public string? HmoCertificationNote { get; set; }

        public string IhsaVerificationStatus { get; set; } = "Not Ready";
        public string? IhsaVerifiedBy { get; set; }
        public DateTime? IhsaVerifiedAt { get; set; }
        public string? IhsaVerificationNote { get; set; }

        public string? OriginalProviderDataJson { get; set; }

        public decimal OutstandingAmount => Math.Max(AmountApproved - AmountPaid, 0m);

        public ICollection<ClaimSupportingDocument> SupportingDocuments { get; set; } = new List<ClaimSupportingDocument>();
        public ICollection<ClaimQuery> Queries { get; set; } = new List<ClaimQuery>();
        public ICollection<ClaimAuditTrail> AuditTrails { get; set; } = new List<ClaimAuditTrail>();
    }
}