namespace CTSHIPDashboard.Models.ViewModels
{
    public class ClaimsViewModel : BaseViewModel
    {
        public List<Claim> Claims { get; set; }
        public int PendingClaims { get; set; }
        public int RejectedClaims { get; set; }
        public double ProcessedWithin30DaysPercentage { get; set; }
        public Dictionary<string, int> RejectionReasons { get; set; } // Reason: Count
    }
}