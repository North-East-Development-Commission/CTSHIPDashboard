using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class AppNotificationRead
{
    public int Id { get; set; }

    public int AppNotificationId { get; set; }
    public AppNotification? AppNotification { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
