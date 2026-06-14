// Models/ViewModels/BaseViewModel.cs
namespace CTSHIPDashboard.Models.ViewModels
{
    public class BaseViewModel
    {
        public int TotalEnrollees { get; set; }
        public int ClaimsProcessed { get; set; }
        public int ActiveProviders { get; set; }
        public decimal TotalFundsManaged { get; set; }
        public int PendingClaims { get; set; }
        public int RejectedClaims { get; set; }
    }
}