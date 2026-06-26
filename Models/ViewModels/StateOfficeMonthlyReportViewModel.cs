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
}
