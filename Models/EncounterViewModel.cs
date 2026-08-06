using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class EncounterViewModel
    {
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        [Display(Name = "Enrollee Number")]
        public string EnrolleeNumber { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        [Range(0, 150)]
        public int? Age { get; set; }

        public string Sex { get; set; } = string.Empty;

        public string Community { get; set; } = string.Empty;

        [Display(Name = "Type of Visit")]
        public string TypeOfVisit { get; set; } = string.Empty;

        [Display(Name = "Presenting Complaints")]
        public List<string> PresentingComplaints { get; set; } = new();

        public string Diagnosis { get; set; } = string.Empty;

        [Display(Name = "Services Provided")]
        public List<string> ServicesProvided { get; set; } = new();

        [Display(Name = "Patient Outcome")]
        public string PatientOutcome { get; set; } = string.Empty;
    }
}