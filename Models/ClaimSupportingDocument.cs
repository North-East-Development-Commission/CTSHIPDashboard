using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class ClaimSupportingDocument
{
    public int Id { get; set; }

    public int ClaimId { get; set; }

    public Claim? Claim { get; set; }

    [Required]
    [StringLength(60)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UploadedByUserId { get; set; }

    [StringLength(200)]
    public string? UploadedByName { get; set; }
}
