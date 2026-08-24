using CTSHIPDashboard.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class Complaint
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(3000)]
        public string Description { get; set; } = string.Empty;

        public ComplaintCategory Category { get; set; } = ComplaintCategory.ServiceDelivery;
        public ComplaintPriority Priority { get; set; } = ComplaintPriority.Medium;
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

        [Required]
        [StringLength(50)]
        public string ComplainantCategory { get; set; } = "Enrollee";

        [Required]
        [StringLength(80)]
        public string CommunicationChannel { get; set; } = "In person";

        [Required]
        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Lga { get; set; }

        public int? HmoId { get; set; }
        public Hmo? Hmo { get; set; }

        public int? ProviderId { get; set; }
        public Provider? Provider { get; set; }

        public int? EnrolleeId { get; set; }
        public Enrollee? Enrollee { get; set; }

        [StringLength(450)]
        public string? SubmittedByUserId { get; set; }

        [StringLength(200)]
        public string? SubmittedByName { get; set; }

        [StringLength(100)]
        public string? SubmittedByRole { get; set; }

        [StringLength(450)]
        public string? AssignedToUserId { get; set; }

        [StringLength(200)]
        public string? AssignedToName { get; set; }

        [StringLength(200)]
        public string? ResponsibleOrganization { get; set; }

        [StringLength(2000)]
        public string? ActionTaken { get; set; }

        [StringLength(2000)]
        public string? EscalationDetails { get; set; }

        [StringLength(2000)]
        public string? ResolutionNote { get; set; }

        [StringLength(1000)]
        public string? ComplainantFeedback { get; set; }

        public DateTime DateReceived { get; set; } = DateTime.UtcNow;
        public DateTime? AgreedResolutionDueAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        public int? ResolutionTimeDays => ResolvedAt.HasValue
            ? (ResolvedAt.Value.Date - DateReceived.Date).Days
            : null;

        public bool? ResolvedWithinAgreedTime => ResolvedAt.HasValue && AgreedResolutionDueAt.HasValue
            ? ResolvedAt.Value <= AgreedResolutionDueAt.Value
            : null;
    }
}