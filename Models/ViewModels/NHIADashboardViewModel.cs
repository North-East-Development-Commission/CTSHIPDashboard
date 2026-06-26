// ViewModels/NHIADashboardViewModel.cs
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class NHIADashboardViewModel
    {
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int TotalClaims { get; set; }
        public int PaidClaims { get; set; }
        public int TotalHMOs { get; set; }
        public int TotalProviders { get; set; }
        public decimal TotalClaimAmount { get; set; }

        public List<StateSummary> StateSummaries { get; set; } = new();
        public List<RecentEnrollee> RecentEnrollees { get; set; } = new();
    }

    public class StateSummary
    {
        public string StateName { get; set; } = string.Empty;
        public int Enrollees { get; set; }
        public int Claims { get; set; }
        public int Providers { get; set; }
        public decimal ClaimAmount { get; set; }
    }
}