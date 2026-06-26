using CTSHIPDashboard.Enums;

namespace CTSHIPDashboard.ViewModels;

public class ReferralAuditLogViewModel
{
    public ReferralAuditAction Action { get; set; }

    public string? PerformedByName { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
