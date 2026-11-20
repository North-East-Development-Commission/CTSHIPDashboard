using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public sealed class GalleryPhoto
{
    public const int MaxDescriptionLength = 500;

    public int Id { get; set; }

    [MaxLength(MaxDescriptionLength)]
    public required string Description { get; set; }

    [MaxLength(10)]
    public required string ContentType { get; set; }

    public required byte[] Image { get; set; }

    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();
}
