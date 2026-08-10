using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class ClaimQuery
{
    public int Id { get; set; }

    public int ClaimId { get; set; }
    public Claim? Claim { get; set; }

    [Required, StringLength(40)]
    public string QueryNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Status { get; set; } = "Open";

    [Required, StringLength(2000)]
    public string QueryRaised { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ResponsiblePerson { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Response { get; set; }

    [StringLength(2000)]
    public string? Resolution { get; set; }

    [StringLength(1000)]
    public string? ClosureNote { get; set; }

    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? RaisedByName { get; set; }

    public DateTime? RespondedAt { get; set; }

    [StringLength(200)]
    public string? RespondedByName { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [StringLength(200)]
    public string? ResolvedByName { get; set; }

    public DateTime? ClosedAt { get; set; }

    [StringLength(200)]
    public string? ClosedByName { get; set; }
}

