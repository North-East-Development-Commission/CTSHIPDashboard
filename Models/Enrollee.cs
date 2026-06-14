using CTSHIPDashboard.Models;
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
        public virtual ICollection<Claim>? Claims { get; set; }
        public virtual ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();

        public ICollection<MedicalHistory>? MedicalHistories { get; set; }
    
    }
 }
