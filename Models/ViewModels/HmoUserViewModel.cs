using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Models.ViewModels;

public class HmoUserViewModel
{
    public string? Id { get; set; }
    [Required, StringLength(200)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Phone] public string? Phone { get; set; }
    [Range(1, int.MaxValue)] public int ProviderId { get; set; }
    public List<string> Roles { get; set; } = new();
    [DataType(DataType.Password)] public string? Password { get; set; }
    public bool Enabled { get; set; } = true;
    public List<SelectListItem> Facilities { get; set; } = new();
}
