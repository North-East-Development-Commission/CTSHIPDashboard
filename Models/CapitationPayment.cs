using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models;

public class CapitationPayment
{
    public int Id { get; set; }

    public int HmoId { get; set; }
    public Hmo? Hmo { get; set; }

    public int ProviderId { get; set; }
    public Provider? Provider { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReportingMonth { get; set; }

    [StringLength(20)]
    public string PaymentPeriod { get; set; } = "Monthly";

    public int EnrolleeCount { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CapitationPerEnrollee { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal UtilizationRate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal ActualPaymentMade { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ProviderPaymentReceivedDate { get; set; }

    [StringLength(50)]
    public string PaymentStatus { get; set; } = "Pending";

    [StringLength(100)]
    public string? PaymentReference { get; set; }

    [StringLength(500)]
    public string? ProofOfPaymentPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    public decimal TotalAmount => EnrolleeCount * CapitationPerEnrollee;

    [NotMapped]
    public decimal OutstandingAmount => Math.Max(TotalAmount - ActualPaymentMade, 0m);

    [NotMapped]
    public int? PaymentTimelinessDays => DueDate.HasValue && ProviderPaymentReceivedDate.HasValue
        ? (ProviderPaymentReceivedDate.Value.Date - DueDate.Value.Date).Days
        : null;
}