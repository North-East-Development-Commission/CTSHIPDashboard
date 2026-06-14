using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
  
    public class StateOfficeDashboardViewModel
    {
        public string StateName { get; set; } = string.Empty;
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int TotalClaims { get; set; }
        public int PaidClaims { get; set; }
        public int HmoCount { get; set; }
        public List<EnrolleeSummaryViewModel> RecentEnrollees { get; set; } = new();
    }

    public class EnrolleeSummaryViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EnrollmentNumber { get; set; } = string.Empty;
        public string HmoName { get; set; } = string.Empty;
        public DateTime DateRegistered { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
