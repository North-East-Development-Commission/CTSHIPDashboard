using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace CTSHIPDashboard.Helpers;

public static class EnrolleePhotoStorage
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static async Task<string> SaveAsync(
        IFormFile photo,
        string enrollmentNumber,
        string? existingPhotoPath,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (photo.Length == 0)
        {
            throw new InvalidOperationException("The selected photo is empty.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            throw new InvalidOperationException("The passport photo must not exceed 5 MB.");
        }

        string extension = Path.GetExtension(photo.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Use a JPG, PNG, or WebP passport photo.");
        }

        string uploadsFolder = Path.Combine(environment.WebRootPath, "uploads", "enrollees");
        Directory.CreateDirectory(uploadsFolder);

        string safeEnrollmentNumber = string.Concat(
            enrollmentNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
        string fileName = $"{safeEnrollmentNumber}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string filePath = Path.Combine(uploadsFolder, fileName);

        await using (FileStream stream = new(filePath, FileMode.CreateNew))
        {
            await photo.CopyToAsync(stream, cancellationToken);
        }

        DeleteExistingPhoto(existingPhotoPath, uploadsFolder);
        return $"/uploads/enrollees/{fileName}";
    }

    private static void DeleteExistingPhoto(string? existingPhotoPath, string uploadsFolder)
    {
        if (string.IsNullOrWhiteSpace(existingPhotoPath))
        {
            return;
        }

        string fullUploadsFolder = Path.GetFullPath(uploadsFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string existingFile = Path.GetFullPath(
            Path.Combine(uploadsFolder, Path.GetFileName(existingPhotoPath)));

        if (existingFile.StartsWith(fullUploadsFolder, StringComparison.OrdinalIgnoreCase)
            && File.Exists(existingFile))
        {
            File.Delete(existingFile);
        }
    }
}
