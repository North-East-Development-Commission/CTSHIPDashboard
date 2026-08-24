using CTSHIPDashboard.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class ComplaintCreateViewModel
    {
        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(3000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public ComplaintCategory Category { get; set; } = ComplaintCategory.ServiceDelivery;

        [Required]
        public ComplaintPriority Priority { get; set; } = ComplaintPriority.Medium;

        [Required]
        [StringLength(50)]
        [Display(Name = "Complainant Category")]
        public string ComplainantCategory { get; set; } = "Enrollee";

        [Required]
        [StringLength(80)]
        [Display(Name = "Communication Channel")]
        public string CommunicationChannel { get; set; } = "In person";

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Date Received")]
        public DateTime DateReceived { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "LGA")]
        public string? Lga { get; set; }

        [Display(Name = "HMO")]
        public int? HmoId { get; set; }

        [Display(Name = "Provider / Facility")]
        public int? ProviderId { get; set; }

        [Display(Name = "Enrollee")]
        public int? EnrolleeId { get; set; }

        public bool StateLocked { get; set; }
        public bool HmoLocked { get; set; }
        public bool ProviderLocked { get; set; }
        public List<SelectListItem> States { get; set; } = new();
        public List<SelectListItem> Hmos { get; set; } = new();
        public List<SelectListItem> Providers { get; set; } = new();
        public List<SelectListItem> Enrollees { get; set; } = new();
        public List<SelectListItem> ComplainantCategories { get; set; } = new();
        public List<SelectListItem> CommunicationChannels { get; set; } = new();
    }

    public class ComplaintUpdateViewModel
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;

        [Required]
        public ComplaintStatus Status { get; set; }

        [Required]
        public ComplaintPriority Priority { get; set; }

        [Display(Name = "Responsible Officer")]
        [StringLength(200)]
        public string? AssignedToName { get; set; }

        [Display(Name = "Responsible Organisation")]
        [StringLength(200)]
        public string? ResponsibleOrganization { get; set; }

        [Display(Name = "Action Taken")]
        [StringLength(2000)]
        public string? ActionTaken { get; set; }

        [Display(Name = "Escalation")]
        [StringLength(2000)]
        public string? EscalationDetails { get; set; }

        [Display(Name = "Resolution / Management Note")]
        [StringLength(2000)]
        public string? ResolutionNote { get; set; }

        [Display(Name = "Agreed Resolution Due Date")]
        [DataType(DataType.DateTime)]
        public DateTime? AgreedResolutionDueAt { get; set; }

        [Display(Name = "Complainant Feedback")]
        [StringLength(1000)]
        public string? ComplainantFeedback { get; set; }
    }

    public class ComplaintMetricsViewModel
    {
        public int TotalComplaints { get; set; }
        public int OpenComplaints { get; set; }
        public int InProgressComplaints { get; set; }
        public int ResolvedComplaints { get; set; }
        public int CriticalComplaints { get; set; }
        public int UnresolvedComplaints { get; set; }
        public int RecurrentFacilityComplaints { get; set; }
        public int RecurrentHmoComplaints { get; set; }
        public decimal ResolutionRate { get; set; }
        public decimal ResolvedWithinAgreedTimeRate { get; set; }
    }
}