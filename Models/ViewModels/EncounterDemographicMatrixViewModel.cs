namespace CTSHIPDashboard.Models.ViewModels
{
    public class EncounterDemographicMatrixViewModel
    {
        public string ScopeLabel { get; set; } = "All states";
        public int TotalEnrollees { get; set; }
        public int UniqueEnrolleesWithEncounters { get; set; }
        public int TotalEncounters { get; set; }
        public List<EncounterDemographicMatrixRowViewModel> Rows { get; set; } = new();
    }

    public class EncounterDemographicMatrixRowViewModel
    {
        public string Dimension { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Enrollees { get; set; }
        public int EnrolleesWithEncounters { get; set; }
        public int Encounters { get; set; }
        public decimal EnrolleeShare { get; set; }
        public decimal EncounterShare { get; set; }
        public decimal EncountersPerThousandEnrollees { get; set; }
    }
}
