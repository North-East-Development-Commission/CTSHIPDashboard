using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class ReferralPriceCatalogItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    [StringLength(200)]
    public string? CreatedByName { get; set; }
}
