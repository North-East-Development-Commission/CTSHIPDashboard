using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterVerificationViewModel : IValidatableObject
    {
        public Guid Id { get; set; }

        public string EnrolleeNumber { get; set; } = string.Empty;

        public string EnrolleeFullName { get; set; } = string.Empty;

        public string ProviderName { get; set; } = string.Empty;

        public DateTime DateOfDeath { get; set; }

        public string CauseOfDeath { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Verification Decision")]
        public bool? IsVerified { get; set; } = true;

        [Display(Name = "HMO Verification Note")]
        [StringLength(1000)]
        public string? HmoVerificationNote { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsVerified == false && string.IsNullOrWhiteSpace(HmoVerificationNote))
            {
                yield return new ValidationResult(
                    "Enter a reason when rejecting a death register.",
                    new[] { nameof(HmoVerificationNote) });
            }
        }
    }

}
