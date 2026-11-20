using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

public sealed class GalleryController(ApplicationDbContext context) : Controller
{
    public const string ManagementRoles = "Admin,CTSHIPAdmin";
    private const int MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxRequestBytes = 6 * 1024 * 1024;

    [HttpGet]
    [AllowAnonymous]
    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        ShowGalleryAsync(cancellationToken);

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var photo = await context.GalleryPhotos.AsNoTracking()
            .Where(photo => photo.Id == id)
            .Select(photo => new { photo.Image, photo.ContentType })
            .SingleOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Image, photo.ContentType);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [Authorize(Roles = ManagementRoles)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload(
        [Bind(Prefix = nameof(GalleryPageViewModel.Upload))] GalleryPhotoInput input,
        CancellationToken cancellationToken)
    {
        if (input.Photo is null)
        {
            ModelState.AddModelError("Upload.Photo", "Choose a photo to upload.");
        }

        if (!ModelState.IsValid || input.Photo is null)
        {
            return await ShowGalleryAsync(cancellationToken, upload: input);
        }

        var image = await ReadImageAsync(input.Photo, "Upload.Photo", cancellationToken);
        if (image is null)
        {
            return await ShowGalleryAsync(cancellationToken, upload: input);
        }

        context.GalleryPhotos.Add(new GalleryPhoto
        {
            Description = input.Description.Trim(),
            ContentType = image.Value.ContentType,
            Image = image.Value.Bytes
        });
        await context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Photo published to the gallery.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Edit(
        int id,
        [Bind(Prefix = nameof(GalleryPageViewModel.Edit))] GalleryPhotoInput input,
        CancellationToken cancellationToken)
    {
        var photo = await context.GalleryPhotos.FindAsync([id], cancellationToken);
        if (photo is null || photo.Version != input.Version)
        {
            return PhotoChanged();
        }

        if (!ModelState.IsValid)
        {
            return await ShowGalleryAsync(cancellationToken, editingId: id, edit: input);
        }

        if (input.Photo is not null)
        {
            var image = await ReadImageAsync(input.Photo, "Edit.Photo", cancellationToken);
            if (image is null)
            {
                return await ShowGalleryAsync(cancellationToken, editingId: id, edit: input);
            }

            photo.Image = image.Value.Bytes;
            photo.ContentType = image.Value.ContentType;
        }

        photo.Description = input.Description.Trim();
        photo.Version = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return PhotoChanged();
        }

        TempData["Success"] = "Gallery photo updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, Guid version, CancellationToken cancellationToken)
    {
        var deleted = await context.GalleryPhotos
            .Where(photo => photo.Id == id && photo.Version == version)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return PhotoChanged();
        }

        TempData["Success"] = "Gallery photo deleted.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult PhotoChanged()
    {
        TempData["Error"] = "The photo was updated or removed. Review the gallery and try again.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ShowGalleryAsync(
        CancellationToken cancellationToken,
        GalleryPhotoInput? upload = null,
        int? editingId = null,
        GalleryPhotoInput? edit = null)
    {
        var photos = await context.GalleryPhotos.AsNoTracking()
            .OrderByDescending(photo => photo.Id)
            .Select(photo => new GalleryPhotoCard(photo.Id, photo.Description, photo.Version))
            .ToListAsync(cancellationToken);

        return View(nameof(Index), new GalleryPageViewModel(photos, upload ?? new GalleryPhotoInput(), editingId, edit));
    }

    private async Task<(byte[] Bytes, string ContentType)?> ReadImageAsync(
        IFormFile photo, string field, CancellationToken cancellationToken)
    {
        if (photo.Length is 0 or > MaxPhotoBytes)
        {
            ModelState.AddModelError(field, "Choose a non-empty photo up to 5 MB.");
            return null;
        }

        var bytes = new byte[(int)photo.Length];
        await using var stream = photo.OpenReadStream();
        await stream.ReadExactlyAsync(bytes, cancellationToken);

        var contentType = GetImageContentType(bytes);
        if (contentType is null)
        {
            ModelState.AddModelError(field, "Choose a JPEG or PNG photo.");
            return null;
        }

        return (bytes, contentType);
    }

    private static string? GetImageContentType(ReadOnlySpan<byte> image)
    {
        ReadOnlySpan<byte> jpegHeader = [0xff, 0xd8, 0xff];
        ReadOnlySpan<byte> jpegEnd = [0xff, 0xd9];
        if (image.Length >= 5 && image.StartsWith(jpegHeader) && image.EndsWith(jpegEnd))
        {
            return "image/jpeg";
        }

        ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
        ReadOnlySpan<byte> pngEnd = [0, 0, 0, 0, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82];
        return image.Length >= 45 && image.StartsWith(pngHeader) && image.EndsWith(pngEnd) ? "image/png" : null;
    }
}
