namespace CTSHIPDashboard.Models.ViewModels
{
    // Models/ViewModels/UserActivityReportViewModel.cs
    public class UserActivityReportViewModel
    {
        public string UserEmail { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public DateTime? LastSeen { get; set; }
        public int TotalLogins { get; set; }
    }

    // Models/ViewModels/UserActivityDashboardViewModel.cs
    public class UserActivityDashboardViewModel
    {
        public List<UserActivity> RecentActivities { get; set; } = new();
        public List<UserActivityReportViewModel> UserStats { get; set; } = new();
        public int TotalLoginsToday { get; set; }
        public int ActiveUsersLast7Days { get; set; }
    }
}
