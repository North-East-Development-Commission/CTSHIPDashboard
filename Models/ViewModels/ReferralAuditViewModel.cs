using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.ViewModels;

public class ReferralAuditViewModel
{
    public Guid ReferralId { get; set; }

    [Required]
    [StringLength(1000)]
    [Display(Name = "Audit Note")]
    public string AuditNote { get; set; } = string.Empty;
}
