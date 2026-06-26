using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.ViewModels;

public class ReferralVerificationViewModel
{
    public Guid ReferralId { get; set; }

    public bool IsApproved { get; set; }

    [Required]
    [StringLength(1000)]
    [Display(Name = "Verification Note")]
    public string VerificationNote { get; set; } = string.Empty;
}
