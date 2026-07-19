using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace CTSHIPDashboard.Helpers;

public static class EnrolleePhotoStorage
{
    public const string DefaultPhotoPath = "/img/icon-192.png";

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

        string uploadsFolder = GetUploadFolder(environment);
        Directory.CreateDirectory(uploadsFolder);

        string safeEnrollmentNumber = SanitizeEnrollmentNumber(enrollmentNumber);
        string fileName = $"{safeEnrollmentNumber}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string filePath = Path.Combine(uploadsFolder, fileName);

        await using (FileStream stream = new(filePath, FileMode.CreateNew))
        {
            await photo.CopyToAsync(stream, cancellationToken);
        }

        DeleteExistingPhotos(uploadsFolder, safeEnrollmentNumber, filePath);
        return $"/uploads/enrollees/{fileName}";
    }

    public static string ResolvePhotoPath(string? storedPhotoPath, string? enrollmentNumber)
    {
        if (PhotoFileExists(storedPhotoPath))
        {
            return storedPhotoPath!;
        }

        if (string.IsNullOrWhiteSpace(enrollmentNumber))
        {
            return DefaultPhotoPath;
        }

        string uploadsFolder = GetUploadFolder();
        if (!Directory.Exists(uploadsFolder))
        {
            return DefaultPhotoPath;
        }

        string safeEnrollmentNumber = SanitizeEnrollmentNumber(enrollmentNumber);
        string? photoPath = Directory
            .EnumerateFiles(uploadsFolder, $"{safeEnrollmentNumber}_*")
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(photoPath)
            ? DefaultPhotoPath
            : $"/uploads/enrollees/{Path.GetFileName(photoPath)}";
    }

    private static bool PhotoFileExists(string? storedPhotoPath)
    {
        if (string.IsNullOrWhiteSpace(storedPhotoPath)
            || !storedPhotoPath.StartsWith("/uploads/enrollees/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string uploadsFolder = GetUploadFolder();
        string fullUploadsFolder = Path.GetFullPath(uploadsFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string storedFile = Path.GetFullPath(
            Path.Combine(uploadsFolder, Path.GetFileName(storedPhotoPath)));

        return storedFile.StartsWith(fullUploadsFolder, StringComparison.OrdinalIgnoreCase)
            && File.Exists(storedFile);
    }

    private static string GetUploadFolder(IWebHostEnvironment? environment = null)
    {
        string webRootPath = string.IsNullOrWhiteSpace(environment?.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : environment.WebRootPath;

        return Path.Combine(webRootPath, "uploads", "enrollees");
    }

    private static string SanitizeEnrollmentNumber(string enrollmentNumber)
    {
        string safeEnrollmentNumber = string.Concat(
            enrollmentNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));

        return string.IsNullOrWhiteSpace(safeEnrollmentNumber)
            ? "enrollee"
            : safeEnrollmentNumber;
    }

    private static void DeleteExistingPhotos(string uploadsFolder, string safeEnrollmentNumber, string currentPhotoPath)
    {
        string fullUploadsFolder = Path.GetFullPath(uploadsFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (string existingFile in Directory.EnumerateFiles(uploadsFolder, $"{safeEnrollmentNumber}_*"))
        {
            string fullExistingFile = Path.GetFullPath(existingFile);
            if (fullExistingFile.StartsWith(fullUploadsFolder, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullExistingFile, Path.GetFullPath(currentPhotoPath), StringComparison.OrdinalIgnoreCase)
                && AllowedExtensions.Contains(Path.GetExtension(fullExistingFile))
                && File.Exists(fullExistingFile))
            {
                File.Delete(fullExistingFile);
            }
        }
    }
}
