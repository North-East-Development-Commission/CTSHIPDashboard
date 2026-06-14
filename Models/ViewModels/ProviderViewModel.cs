namespace CTSHIPDashboard.Models.ViewModels
{
    public class ProviderViewModel : BaseViewModel
    {
        public List<Provider> Providers { get; set; }
        public int ActiveFacilities { get; set; }
        public double AveragePatientRatio { get; set; }
        public Dictionary<string, int> ProvidersByLocation { get; set; }
    }
}