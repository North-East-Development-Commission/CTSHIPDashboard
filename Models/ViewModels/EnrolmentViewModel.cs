namespace CTSHIPDashboard.Models.ViewModels
{
    public class EnrolmentViewModel : BaseViewModel
    {
        public List<Enrollee> Enrollees { get; set; }
        public Dictionary<string, int> EnrolleesByState { get; set; }
        public Dictionary<string, int> MonthlyTrends { get; set; } // e.g., "2023-01": count
        public double UnderservedPercentage { get; set; } // % from low-income, rural, etc.
    }
}