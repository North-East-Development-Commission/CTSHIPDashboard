namespace CTSHIPDashboard.Models.ViewModels
{
    public class EnrollmentAnalyticsViewModel
    {
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public decimal FemalePercentage { get; set; }
        public decimal CoverageRate { get; set; }

        public Dictionary<string, int> AgeGroups { get; set; } = new();
        public List<ChartData> EnrollmentByState { get; set; } = new();
        public List<ChartData> EnrollmentByHMO { get; set; } = new();
        public List<ChartData> RegistrationTrend { get; set; } = new();
    }
}
