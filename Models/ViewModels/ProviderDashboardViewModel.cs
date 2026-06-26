using CTSHIPDashboard.Models;
using System.Collections.Generic;

namespace CTSHIPDashboard.ViewModels
{
    public class ProviderDashboardViewModel
    {
        // Provider Details
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderCode { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        // Stats
        public int TotalUniqueEnrollees { get; set; }
        public int TotalDoctors { get; set; }
        public int TotalEncounters { get; set; }
        public int TotalClaims { get; set; }
        public decimal TotalClaimAmount { get; set; }
        public int PendingClaims { get; set; }
        public int PaidClaims { get; set; }

        // Recent Encounters (Latest 10)
        public List<Encounter> RecentEncounters { get; set; } = new();

        // All Claims for this Provider
        public List<Claim> Claims { get; set; } = new();

        // Unique Enrollees Treated at this Provider
        public List<Enrollee> Enrollees { get; set; } = new();

        // Top Doctors Performance (using AttendedBy string)
        public List<TopDoctorStats> TopDoctors { get; set; } = new();
        public List<CTSHIPDashboard.Models.ViewModels.ServiceFrequencyViewModel> MostUsedServices { get; set; } = new();
    }

    public class TopDoctorStats
    {
        public string DoctorName { get; set; } = string.Empty;
        public int EncounterCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
