using System.ComponentModel.DataAnnotations;
using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.Models;

public class ReferralAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReferralId { get; set; }

    public Referral? Referral { get; set; }

    public ReferralAuditAction Action { get; set; }

    [StringLength(450)]
    public string? PerformedByUserId { get; set; }

    [StringLength(200)]
    public string? PerformedByName { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
