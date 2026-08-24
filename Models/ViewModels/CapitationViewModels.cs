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
    public decimal TotalActualPayment => Providers.Sum(provider => provider.ActualPaymentMade);
    public decimal TotalOutstandingAmount => Providers.Sum(provider => provider.OutstandingAmount);
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
    public string PaymentPeriod { get; set; } = "Monthly";
    public int EnrolleeCount { get; set; }
    public decimal CapitationPerEnrollee { get; set; }
    public decimal TotalCapitation => EnrolleeCount * CapitationPerEnrollee;
    public decimal UtilizationRate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal ActualPaymentMade { get; set; }
    public DateTime? ProviderPaymentReceivedDate { get; set; }
    public decimal OutstandingAmount => Math.Max(TotalCapitation - ActualPaymentMade, 0m);
    public int? PaymentTimelinessDays => DueDate.HasValue && ProviderPaymentReceivedDate.HasValue
        ? (ProviderPaymentReceivedDate.Value.Date - DueDate.Value.Date).Days
        : null;
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

    [Required]
    [StringLength(20)]
    public string PaymentPeriod { get; set; } = "Monthly";

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CapitationPerEnrollee { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal ActualPaymentMade { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ProviderPaymentReceivedDate { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentStatus { get; set; } = "Pending";

    [StringLength(100)]
    public string? PaymentReference { get; set; }
}