namespace CTSHIPDashboard.Models.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        public List<NewsUpdate> News { get; set; }
        public string ProjectOverview { get; set; } = "Brief introduction to the CHI project, its objectives, and benefits.";

        // Additional KPIs from specs
        public int AccreditedProviders { get; set; } // Number of active HCPs
        public decimal TotalCapitationPaid { get; set; } // Sum of processed claims amounts
        public int TotalClaimsProcessed { get; set; }
        public decimal TotalFundsManaged { get; set; } // e.g., Sum of all claims amounts
        public int TotalEnrollees { get; set; } // Already had, but explicit
    }
}