using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Helpers;

public static class ReferralProviderSyncHelper
{
    public const string SecondaryProviderLevel = "Secondary";
    public const string ReferralProviderLevel = "Referral Hospital";

    public static bool IsReferralProviderLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return false;
        }

        string normalizedLevel = level.Trim();
        return string.Equals(normalizedLevel, SecondaryProviderLevel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLevel, ReferralProviderLevel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLevel, "Referral Provider", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLevel, "Referred Hospital", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLevel, "Referred Provider", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReferralProvider(Provider? provider)
    {
        return provider?.IsActive == true && IsReferralProviderLevel(provider.Level);
    }

    public static async Task<ReferredHospital?> EnsureReferralHospitalForProviderAsync(
        ApplicationDbContext context,
        Provider provider,
        CancellationToken cancellationToken = default)
    {
        if (!IsReferralProvider(provider))
        {
            return null;
        }

        string providerName = provider.Name.Trim();
        string providerState = provider.State.Trim();
        string? providerEmail = string.IsNullOrWhiteSpace(provider.Email) ? null : provider.Email.Trim();

        ReferredHospital? hospital = await context.ReferralHospitals
            .FirstOrDefaultAsync(hospital =>
                hospital.IsActive &&
                ((!string.IsNullOrWhiteSpace(providerEmail) && hospital.Email == providerEmail) ||
                 (hospital.Name == providerName && hospital.State == providerState)),
                cancellationToken);

        bool changed = false;
        if (hospital == null)
        {
            hospital = new ReferredHospital
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            context.ReferralHospitals.Add(hospital);
            changed = true;
        }

        changed |= SetIfDifferent(value => hospital.Name = value ?? string.Empty, hospital.Name, providerName);
        changed |= SetIfDifferent(value => hospital.State = value, hospital.State, providerState);
        changed |= SetIfDifferent(value => hospital.Lga = value, hospital.Lga, provider.LGA);
        changed |= SetIfDifferent(value => hospital.Address = value, hospital.Address, provider.Location);
        changed |= SetIfDifferent(value => hospital.PhoneNumber = value, hospital.PhoneNumber, provider.Phone);
        changed |= SetIfDifferent(value => hospital.Email = value, hospital.Email, providerEmail);

        if (!hospital.IsActive)
        {
            hospital.IsActive = true;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return hospital;
    }

    public static async Task EnsureReferralHospitalsForSecondaryProvidersAsync(
        ApplicationDbContext context,
        int? hmoId = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Provider> query = context.Providers
            .Where(provider =>
                provider.IsActive &&
                provider.Level == SecondaryProviderLevel);

        if (hmoId.HasValue)
        {
            query = query.Where(provider => provider.HmoId == hmoId.Value);
        }

        List<Provider> providers = await query.ToListAsync(cancellationToken);
        foreach (Provider provider in providers)
        {
            await EnsureReferralHospitalForProviderAsync(context, provider, cancellationToken);
        }
    }

    public static bool MatchesProvider(ReferredHospital hospital, Provider provider)
    {
        bool emailMatches = !string.IsNullOrWhiteSpace(hospital.Email)
            && !string.IsNullOrWhiteSpace(provider.Email)
            && string.Equals(hospital.Email.Trim(), provider.Email.Trim(), StringComparison.OrdinalIgnoreCase);

        bool nameAndStateMatch = string.Equals(hospital.Name?.Trim(), provider.Name?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(hospital.State?.Trim(), provider.State?.Trim(), StringComparison.OrdinalIgnoreCase);

        return emailMatches || nameAndStateMatch;
    }

    private static bool SetIfDifferent(Action<string?> setValue, string? currentValue, string? nextValue)
    {
        nextValue = string.IsNullOrWhiteSpace(nextValue) ? null : nextValue.Trim();
        currentValue = string.IsNullOrWhiteSpace(currentValue) ? null : currentValue.Trim();

        if (string.Equals(currentValue, nextValue, StringComparison.Ordinal))
        {
            return false;
        }

        setValue(nextValue);
        return true;
    }
}
