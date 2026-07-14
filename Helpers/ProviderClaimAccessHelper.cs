using CTSHIPDashboard.Models;

namespace CTSHIPDashboard.Helpers;

public static class ProviderClaimAccessHelper
{
    public static readonly string[] ClaimEligibleProviderLevels =
    {
        "Secondary",
        "Referral Hospital",
        "Referred Hospital",
        "Referral Provider",
        "Referred Provider"
    };

    public const string ClaimsUnavailableMessage =
        "Primary Healthcare Center (PHC) providers cannot use claims services. Claims are available only to secondary or referred providers.";

    public static bool CanUseClaims(string? providerLevel)
    {
        if (string.IsNullOrWhiteSpace(providerLevel))
        {
            return false;
        }

        string normalizedLevel = providerLevel.Trim();
        return ClaimEligibleProviderLevels.Any(level =>
            string.Equals(level, normalizedLevel, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanUseClaims(Provider? provider)
    {
        return CanUseClaims(provider?.Level);
    }

    public static IQueryable<Claim> WhereProviderCanUseClaims(this IQueryable<Claim> claims)
    {
        return claims.Where(claim =>
            claim.Provider != null &&
            ClaimEligibleProviderLevels.Contains(claim.Provider.Level));
    }
}
