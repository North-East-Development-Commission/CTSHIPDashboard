using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class AppNotification
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string TargetGroup { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EventName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Url { get; set; }

    [StringLength(40)]
    public string Icon { get; set; } = "info";

    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppNotificationRead> Reads { get; set; } = new();
}
