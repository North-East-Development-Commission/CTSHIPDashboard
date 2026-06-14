using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public static class SeedDataHelper
    {
        // Reuse small helper to provide the same states list used in seeding.
        public static string[] GetNigerianStates() => new[] { "Adamawa", "Bauchi", "Borno", "Gombe", "Taraba", "Yobe" };
    }

    // StateOffice dashboard view models moved to Models/ViewModels/DashboardViewModels.cs to avoid duplicates

    public class CreateStateOfficerViewModel
    {
        [Required, EmailAddress] public string Name { get; set; }       
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string State { get; set; } = string.Empty;

        [Required, DataType(DataType.PhoneNumber)]
        public string Phone { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = "State@2025";
    }
}
