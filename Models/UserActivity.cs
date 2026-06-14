// Models/UserActivity.cs
using CTSHIPDashboard.Models;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class UserActivity
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Action { get; set; } = "Login"; // Login, Logout, ViewDashboard, etc.

        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}