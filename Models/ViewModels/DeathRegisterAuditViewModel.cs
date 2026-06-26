using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class DeathRegisterAuditViewModel : IValidatableObject
    {
        public Guid Id { get; set; }

        public string EnrolleeNumber { get; set; } = string.Empty;

        public string EnrolleeFullName { get; set; } = string.Empty;

        public string ProviderName { get; set; } = string.Empty;

        public DateTime DateOfDeath { get; set; }

        public string CauseOfDeath { get; set; } = string.Empty;

        public string? HmoVerificationNote { get; set; }

        [Required]
        [Display(Name = "Audit Decision")]
        public bool? IsApproved { get; set; } = true;

        [Display(Name = "Audit Note")]
        [StringLength(1000)]
        public string? AuditNote { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsApproved == false && string.IsNullOrWhiteSpace(AuditNote))
            {
                yield return new ValidationResult(
                    "Enter a reason when rejecting the audit.",
                    new[] { nameof(AuditNote) });
            }
        }
    }

}
