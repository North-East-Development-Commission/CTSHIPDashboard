using CTSHIPDashboard.Models;

namespace CTSHIPDashboard.Helpers;

public static class AuditActor
{
    public static string Format(ApplicationUser? user, string? fallback = null)
    {
        if (user == null)
        {
            return Clean(fallback) ?? "Unknown";
        }

        string? email = Clean(user.Email ?? user.UserName);
        string? name = Clean(user.FullName);

        if (name != null && email != null)
        {
            return $"{name} ({email})";
        }

        return name ?? email ?? Clean(fallback) ?? user.Id;
    }

    public static string Format(string? actor) => Clean(actor) ?? "Unknown";

    public static string? Details(params string?[] parts)
    {
        string detail = string.Join("; ", parts
            .Select(Clean)
            .Where(part => part != null));

        return string.IsNullOrWhiteSpace(detail) ? null : detail;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }
}
