using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CTSHIPDashboard.Helpers;

public static class ProfilePhotoStorage
{
    public const string DefaultPhotoPath = "/img/icon-192.png";

    private const long MaxProfilePhotoBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static async Task SaveAsync(
        IFormFile photo,
        string userId,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (photo.Length == 0)
        {
            throw new InvalidOperationException("The selected profile photo is empty.");
        }

        if (photo.Length > MaxProfilePhotoBytes)
        {
            throw new InvalidOperationException("The profile photo must not exceed 2 MB.");
        }

        string extension = Path.GetExtension(photo.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Use a JPG, PNG, or WebP profile photo.");
        }

        string uploadFolder = GetUploadFolder(environment);
        Directory.CreateDirectory(uploadFolder);

        string safeUserId = SanitizeKey(userId);
        string fileName = $"{safeUserId}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string filePath = Path.Combine(uploadFolder, fileName);

        await using FileStream stream = new(filePath, FileMode.CreateNew);
        await photo.CopyToAsync(stream, cancellationToken);

        DeleteExistingPhotos(uploadFolder, safeUserId, filePath);
    }

    public static string ResolvePhotoPath(string userId, IWebHostEnvironment environment)
    {
        string uploadFolder = GetUploadFolder(environment);
        if (!Directory.Exists(uploadFolder))
        {
            return DefaultPhotoPath;
        }

        string safeUserId = SanitizeKey(userId);
        string? profilePhoto = Directory
            .EnumerateFiles(uploadFolder, $"{safeUserId}_*")
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(profilePhoto)
            ? DefaultPhotoPath
            : $"/uploads/profile/{Path.GetFileName(profilePhoto)}";
    }

    private static string GetUploadFolder(IWebHostEnvironment environment)
    {
        string webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : environment.WebRootPath;

        return Path.Combine(webRootPath, "uploads", "profile");
    }

    private static string SanitizeKey(string value)
    {
        string safeValue = string.Concat(value.Where(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'));

        return string.IsNullOrWhiteSpace(safeValue)
            ? "user"
            : safeValue;
    }

    private static void DeleteExistingPhotos(string uploadFolder, string safeUserId, string currentPhotoPath)
    {
        string fullUploadFolder = Path.GetFullPath(uploadFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (string existingFile in Directory.EnumerateFiles(uploadFolder, $"{safeUserId}_*"))
        {
            string fullExistingFile = Path.GetFullPath(existingFile);
            if (fullExistingFile.StartsWith(fullUploadFolder, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullExistingFile, Path.GetFullPath(currentPhotoPath), StringComparison.OrdinalIgnoreCase)
                && AllowedExtensions.Contains(Path.GetExtension(fullExistingFile))
                && System.IO.File.Exists(fullExistingFile))
            {
                System.IO.File.Delete(fullExistingFile);
            }
        }
    }
}
