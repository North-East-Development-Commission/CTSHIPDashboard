using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    [Table("Enrollees")]
    public class Enrollee
    {
        [Key]
        public int Id { get; set; }

        // PERSONAL INFORMATION
        [Required(ErrorMessage = "Enter the enrollee's full name.")]
        [StringLength(200, ErrorMessage = "Full name must be 200 characters or fewer.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(30, ErrorMessage = "Enrollment number must be 30 characters or fewer.")]
        [Display(Name = "Enrollment Number")]
        public string EnrollmentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select the enrollee's gender.")]
        [StringLength(10, ErrorMessage = "Gender must be Male, Female, M, or F.")]
        public string Gender { get; set; } = string.Empty; // Male, Female

        [Required(ErrorMessage = "Enter the enrollee's date of birth.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Phone(ErrorMessage = "Enter a valid phone number, such as 08012345678, or leave it blank.")]
        [StringLength(20, ErrorMessage = "Phone number must be 20 characters or fewer.")]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [StringLength(500, ErrorMessage = "Address must be 500 characters or fewer, or leave it blank.")]
        public string? Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select the enrollee's state.")]
        [StringLength(50, ErrorMessage = "State must be 50 characters or fewer.")]
        public string State { get; set; } = string.Empty; // e.g. Lagos, Kano, FCT
        [Required(ErrorMessage = "Select a ward or enter its name.")]
        [StringLength(50, ErrorMessage = "Ward must be 50 characters or fewer.")]
        public string Ward { get; set; } = string.Empty; // e.g. Lagos, Kano, FCT

        [Range(1, long.MaxValue, ErrorMessage = "Enter a positive NIN using digits only, or leave it blank.")]
        [Display(Name = "NIN")]
        public long? NIN { get; set; }

        [Required(ErrorMessage = "Select an LGA for the selected state.")]
        [StringLength(100, ErrorMessage = "LGA must be 100 characters or fewer.")]
        public string LGA { get; set; } = string.Empty; // Local Government Area

        [Display(Name = "Pregnant Woman")]
        public bool IsPregnant { get; set; }

        [Display(Name = "Person Living with Disability (PLWD)")]
        public bool HasDisability { get; set; }

        [Display(Name = "Internally Displaced Person (IDP)")]
        public bool IsIdp { get; set; }

        [StringLength(100, ErrorMessage = "Other vulnerable category must be 100 characters or fewer.")]
        [Display(Name = "Other Vulnerable Category")]
        public string? OtherVulnerableCategory { get; set; }

        // COVERAGE & HMO
        
        [Display(Name = "HmoId")]
        public int? HmoId { get; set; }

        [ForeignKey("HmoId")]
        public virtual Hmo? Hmo { get; set; }
        [Display(Name = "ProviderId")]
        public int? ProviderId { get; set; }

        [ForeignKey("ProviderId")]
        public virtual Provider? provider { get; set; }

        // SYSTEM FIELDS
        [Required(ErrorMessage = "Select the enrollee's status.")]
        [StringLength(20, ErrorMessage = "Status must be 20 characters or fewer.")]
        public string Status { get; set; } = "Active"; // Active, Inactive, Suspended

        [DataType(DataType.DateTime)]
        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; } = DateTime.Now;

        [StringLength(100)]
        [Display(Name = "Registered By")]
        public string? RegisteredBy { get; set; }

        // PHOTO
        [StringLength(500)]
        [Display(Name = "Photo")]
        public string? PhotoPath { get; set; }
        [NotMapped]
        public IFormFile? PhotoFile { get; set; }

        // NAVIGATION PROPERTIES
        //public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        //admin @nhia.gov.ng / Nigeria@2025!
        public virtual ICollection<Claim>? Claims { get; set; }
        public virtual ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();

        public ICollection<MedicalHistory>? MedicalHistories { get; set; }

        public bool IsActive { get; internal set; }

        // Read-only computed property — perfect!
        public string StatusBadgeClass => Status?.ToLowerInvariant() switch
        {
            "active" => "bg-success",
            "inactive" => "bg-secondary",
            "suspended" => "bg-danger",
            "terminated" => "bg-dark",
            "pending" => "bg-warning text-dark",
            _ => "bg-light text-dark"
        };

        public string StatusDisplay => Status?.ToLowerInvariant() switch
        {
            "active" => "Active",
            "inactive" => "Inactive",
            "suspended" => "Suspended",
            "terminated" => "Terminated",
            "pending" => "Pending Approval",
            _ => Status ?? "Unknown"
        };
        [NotMapped]
        public EnrolleeDeathStatusViewModel DeathStatus { get; set; } = EnrolleeDeathStatusViewModel.Active();
    }
 }
