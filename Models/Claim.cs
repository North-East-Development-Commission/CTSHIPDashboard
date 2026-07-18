// Models/Claim.cs
using CTSHIPDashboard.Models;
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

        [Required]
        public decimal Amount { get; set; }

        public string Diagnosis { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.Now;
        public DateTime? DateProcessed { get; set; }

        [Required]
        public string Status { get; set; } = "Submitted"; // Pending, Paid, Rejected

        public string SubmittedBy { get; set; } = string.Empty;

        // NEW WORKFLOW FIELDS
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

        public ICollection<ClaimSupportingDocument> SupportingDocuments { get; set; } = new List<ClaimSupportingDocument>();
    }
}
