using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.ViewModels;

public class ReferralHospitalViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Hospital Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string? State { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "LGA")]
    public string? Lga { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [StringLength(50)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [StringLength(150)]
    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
