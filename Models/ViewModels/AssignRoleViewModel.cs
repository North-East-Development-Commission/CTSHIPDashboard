// File: Models/AssignRoleViewModel.cs

using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class AssignRoleViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        // Current roles (for display)
        public List<string> CurrentRoles { get; set; } = new();

        // All available roles in the system
        public List<string> AllRoles { get; set; } = new();

        // Roles selected by admin to assign (supports multiple)
        [Display(Name = "Assign Roles (Hold Ctrl/Cmd to select multiple)")]
        public List<string>? SelectedRoles { get; set; } = new();
    }
}