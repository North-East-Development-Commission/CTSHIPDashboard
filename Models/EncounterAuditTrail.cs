using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class EncounterAuditTrail
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [StringLength(200)]
    public string? PerformedByName { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [StringLength(2000)]
    public string? Summary { get; set; }

    public string? OriginalValuesJson { get; set; }

    public string? NewValuesJson { get; set; }
}
