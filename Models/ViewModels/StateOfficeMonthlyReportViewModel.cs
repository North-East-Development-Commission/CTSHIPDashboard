using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class StateOfficeMonthlyReportViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Reporting month is required.")]
        [Display(Name = "Reporting Month")]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Select a valid reporting month.")]
        public string ReportingPeriod { get; set; } = DateTime.Today.ToString("yyyy-MM");

        [Required]
        public string State { get; set; } = string.Empty;

        [Required]
        [Display(Name = "LGA")]
        public string Lga { get; set; } = string.Empty;

        [Required]
        public string Ward { get; set; } = string.Empty;

        [Required(ErrorMessage = "Facility name is required.")]
        [Display(Name = "Facility Name")]
        public int? ProviderId { get; set; }

        [Display(Name = "Facility Code")]
        public string FacilityCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        [Display(Name = "Reporting Officer Name")]
        public string ReportingOfficerName { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Designation { get; set; } = string.Empty;

        [Required, Phone, StringLength(30)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Date Submitted")]
        public DateTime? DateSubmitted { get; set; }

        public List<SelectListItem> States { get; set; } = new();
        public List<SelectListItem> Lgas { get; set; } = new();
        public List<SelectListItem> Wards { get; set; } = new();
        public List<SelectListItem> Facilities { get; set; } = new();
    }

    public class StateOfficeMonthlyReportMetricsViewModel
    {
        public string ReportingPeriod { get; set; } = string.Empty;
        public string ReportingMonthDisplay { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Lga { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public int ProviderId { get; set; }
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityCode { get; set; } = string.Empty;
        public int TotalActiveEnrollees { get; set; }
        public int TotalVisits { get; set; }
        public int TotalEncounters { get; set; }
        public int EnrolleesAccessingCare { get; set; }
        public int ServiceUtilization { get; set; }
        public int TotalReferrals { get; set; }
        public int CompletedReferrals { get; set; }
        public decimal ReferralCompletionRate { get; set; }
        public decimal AmountCapitationPaid { get; set; }
        public decimal CapitationToUtilizationRatio { get; set; }
        public int TotalClaims { get; set; }
        public decimal TotalClaimsAmount { get; set; }
        public int PaidClaims { get; set; }
        public decimal PaidClaimsAmount { get; set; }
    }
}
