using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.ViewModels
{
   
    public class RecentEnrollee
    {
        public string EnrollmentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string HmoName { get; set; } = string.Empty;
        public DateTime DateRegistered { get; set; }
        public string Status { get; set; } = "Active";
        public string State { get; internal set; }
    }
}