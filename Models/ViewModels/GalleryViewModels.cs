using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels;

public sealed record GalleryPhotoCard(int Id, string Description, Guid Version);

public sealed record GalleryPageViewModel(
    IReadOnlyList<GalleryPhotoCard> Photos,
    GalleryPhotoInput Upload,
    int? EditingId = null,
    GalleryPhotoInput? Edit = null);

public sealed class GalleryPhotoInput
{
    public Guid Version { get; set; }

    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "Enter a description for the photo.")]
    [StringLength(GalleryPhoto.MaxDescriptionLength)]
    public string Description { get; set; } = "";
}
