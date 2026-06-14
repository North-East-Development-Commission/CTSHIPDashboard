using System;
using System.Collections.Generic;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class NHIAKpiViewModel
    {
        public int TotalEnrollees { get; set; }
        public int ActiveEnrollees { get; set; }
        public int TotalProviders { get; set; }
        public int TotalHmos { get; set; }

        public int TotalClaims { get; set; }
        public int PaidClaims { get; set; }
        public int PendingClaims { get; set; }
        public decimal TotalPaidAmount { get; set; }

        public List<KeyValuePair<string,int>> EnrolleesByState { get; set; } = new();
        public List<KeyValuePair<string,int>> TopHmos { get; set; } = new();
        public List<KeyValuePair<string,int>> ClaimsByState { get; set; } = new();
    }

    // StateOffice dashboard models are defined in SeedDataHelper.cs to avoid duplicate declarations
}
