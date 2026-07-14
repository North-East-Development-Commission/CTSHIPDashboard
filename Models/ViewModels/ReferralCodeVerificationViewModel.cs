using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.ViewModels;

public class ReferralCodeVerificationViewModel
{
    public Guid? ReferralId { get; set; }

    [Required]
    [StringLength(30)]
    [Display(Name = "Referral Verification Code")]
    public string Code { get; set; } = string.Empty;

    public string? ReferredHospitalName { get; set; }

    public string? EnrolleeNumberHint { get; set; }
}

public class ReferralCodeVerificationResult
{
    public bool Succeeded { get; set; }

    public Guid? ReferralId { get; set; }

    public string Message { get; set; } = string.Empty;

    public static ReferralCodeVerificationResult Success(Guid referralId, string message) =>
        new()
        {
            Succeeded = true,
            ReferralId = referralId,
            Message = message
        };

    public static ReferralCodeVerificationResult Failure(string message) =>
        new()
        {
            Succeeded = false,
            Message = message
        };
}
