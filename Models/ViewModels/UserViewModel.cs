using CTSHIPDashboard.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public Organization? organization { get; set; }
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public int? HmoId { get; set; }
    public Hmo? hmo { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? State { get; set; }
    public string? ContactInfo { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsLocked { get; set; }
}

public class CreateUserViewModel
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }
    public string? State { get; set; }
    public string? ContactInfo { get; set; }
    public string? RegisteredBy { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? organization { get; set; }
    public int? HmoId { get; set; }
    public Hmo? hmo { get; set; }
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public Guid? ReferralHospitalId { get; set; }
    public List<string> AllRoles { get; set; } = new();

    // CHANGED FROM string TO List<string>
    [Required(ErrorMessage = "Please select at least one role")]
    public List<string> SelectedRoles { get; set; }
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? State { get; set; }
    public int? OrganizationId { get; set; }
    [ForeignKey("OrganizationId")] // This tells EF which ID to use for this object
    public Organization? Organization { get; set; }
    public string? ContactInfo { get; set; }
    public int? HmoId { get; set; }
    public Hmo? hmo { get; set; }
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public Guid? ReferralHospitalId { get; set; }
    public List<string> CurrentRoles { get; set; } = new();
    public List<string> AllRoles { get; set; } = new();
    public List<string>? SelectedRoles { get; set; }
}
