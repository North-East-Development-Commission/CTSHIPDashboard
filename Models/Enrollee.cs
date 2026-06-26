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
        [Required]
        [StringLength(200)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]                    // INCREASED FROM 20 TO 30
        [Display(Name = "Enrollment Number")]
        public string EnrollmentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Gender { get; set; } = string.Empty; // Male, Female

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string State { get; set; } = string.Empty; // e.g. Lagos, Kano, FCT
        [Required]
        [StringLength(50)]
        public string Ward { get; set; } = string.Empty; // e.g. Lagos, Kano, FCT

        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "Must be a valid positive long integer.")]
        [Display(Name = "NIN")]
        public long NIN { get; set; }

        [Required]
        [StringLength(100)]
        public string LGA { get; set; } = string.Empty; // Local Government Area

        [Display(Name = "Pregnant Woman")]
        public bool IsPregnant { get; set; }

        [Display(Name = "Person Living with Disability (PLWD)")]
        public bool HasDisability { get; set; }

        [Display(Name = "Internally Displaced Person (IDP)")]
        public bool IsIdp { get; set; }

        [StringLength(100)]
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
        [Required]
        [StringLength(20)]
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
