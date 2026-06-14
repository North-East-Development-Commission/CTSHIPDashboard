using System.ComponentModel.DataAnnotations;
namespace CTSHIPDashboard.Models.ViewModels
{
    public class ProviderListViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = "N/A";
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; }

        public int EnrolleeCount { get; set; }
        public int EncounterCount { get; set; }
        public int ClaimCount { get; set; }
        public decimal TotalRevenue { get; set; } = 0;

        public DateTime DateRegistered { get; set; }

        // Optional: Display helpers
        public string LevelDisplay => Level switch
        {
            "Tertiary" => "Tertiary",
            "Secondary" => "Secondary",
            "Private" => "Private",
            "Primary" => "Primary",
            _ => "Unknown"
        };

        public string StatusBadgeClass => IsActive ? "bg-success" : "bg-danger";
    }
}

