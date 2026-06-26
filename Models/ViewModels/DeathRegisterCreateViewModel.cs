using CTSHIPDashboard.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterCreateViewModel : IValidatableObject
    {
        public int? EnrolleeId { get; set; }

        [Required]
        [Display(Name = "Enrollee Number")]
        [StringLength(100)]
        public string EnrolleeNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Enrollee Full Name")]
        [StringLength(200)]
        public string EnrolleeFullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Phone Number")]
        [Phone]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Display(Name = "HMO Code")]
        [StringLength(100)]
        public string? HmoCode { get; set; }

        [Display(Name = "HMO Name")]
        [StringLength(200)]
        public string? HmoName { get; set; }

        [Display(Name = "Provider Id")]
        [StringLength(100)]
        public string? ProviderId { get; set; }

        [Required]
        [Display(Name = "Provider Name")]
        [StringLength(200)]
        public string ProviderName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Death")]
        public DateTime? DateOfDeath { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Time of Death")]
        public TimeSpan? TimeOfDeath { get; set; }

        [Required]
        [Display(Name = "Place of Death")]
        [StringLength(300)]
        public string PlaceOfDeath { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Cause of Death")]
        [StringLength(1000)]
        public string CauseOfDeath { get; set; } = string.Empty;

        [Display(Name = "Cause Category")]
        [EnumDataType(typeof(DeathCauseCategory))]
        public DeathCauseCategory CauseCategory { get; set; } = DeathCauseCategory.Unknown;

        [Required]
        [Display(Name = "Death Confirmed By")]
        [StringLength(200)]
        public string DeathConfirmedBy { get; set; } = string.Empty;

        [Display(Name = "Confirmer Designation")]
        [StringLength(100)]
        public string? DeathConfirmedByDesignation { get; set; }

        [Display(Name = "Confirmer Phone")]
        [Phone]
        [StringLength(50)]
        public string? DeathConfirmedByPhone { get; set; }

        [Display(Name = "Death Certificate Number")]
        [StringLength(100)]
        public string? DeathCertificateNumber { get; set; }

        [Display(Name = "Death Certificate File Path")]
        [StringLength(500)]
        public string? DeathCertificateFilePath { get; set; }

        [Display(Name = "Provider Remarks")]
        [StringLength(1000)]
        public string? ProviderRemarks { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue && DateOfBirth.Value.Date > DateTime.Today)
            {
                yield return new ValidationResult(
                    "Date of birth cannot be in the future.",
                    new[] { nameof(DateOfBirth) });
            }

            if (DateOfDeath.HasValue && DateOfDeath.Value.Date > DateTime.Today)
            {
                yield return new ValidationResult(
                    "Date of death cannot be in the future.",
                    new[] { nameof(DateOfDeath) });
            }

            if (DateOfBirth.HasValue
                && DateOfDeath.HasValue
                && DateOfDeath.Value.Date < DateOfBirth.Value.Date)
            {
                yield return new ValidationResult(
                    "Date of death cannot be earlier than date of birth.",
                    new[] { nameof(DateOfDeath) });
            }

            if (!string.IsNullOrWhiteSpace(Gender)
                && !new[] { "Male", "Female" }.Contains(Gender, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    "Select a valid gender.",
                    new[] { nameof(Gender) });
            }
        }
    }

}
