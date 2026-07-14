using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels;

public class CapitationIndexViewModel
{
    public string HmoName { get; set; } = string.Empty;
    public string HmoCode { get; set; } = string.Empty;
    public DateTime ReportingMonth { get; set; }
    public decimal DefaultCapitationPerEnrollee { get; set; }
    public List<CapitationProviderRowViewModel> Providers { get; set; } = new();

    public int TotalEnrollees => Providers.Sum(provider => provider.EnrolleeCount);
    public decimal TotalCapitation => Providers.Sum(provider => provider.TotalCapitation);
    public int PaidProviderCount => Providers.Count(provider => provider.PaymentStatus == "Paid");

    public decimal AverageUtilizationRate => TotalEnrollees == 0
        ? 0m
        : Math.Round(Providers.Sum(provider => provider.UtilizationRate * provider.EnrolleeCount) / TotalEnrollees, 2);
}

public class CapitationProviderRowViewModel
{
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderLevel { get; set; } = string.Empty;
    public int EnrolleeCount { get; set; }
    public decimal CapitationPerEnrollee { get; set; }
    public decimal TotalCapitation => EnrolleeCount * CapitationPerEnrollee;
    public decimal UtilizationRate { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaymentReference { get; set; }
    public string? ProofOfPaymentPath { get; set; }
}

public class CapitationPaymentUpdateViewModel
{
    [Required]
    public int ProviderId { get; set; }

    [Required]
    public string ReportingMonth { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CapitationPerEnrollee { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentStatus { get; set; } = "Pending";

    [StringLength(100)]
    public string? PaymentReference { get; set; }
}
