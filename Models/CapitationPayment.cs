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

    public int EnrolleeCount { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CapitationPerEnrollee { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal UtilizationRate { get; set; }

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
}
