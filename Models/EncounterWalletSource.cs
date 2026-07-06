namespace CTSHIPDashboard.Models
{
    public static class EncounterWalletSource
    {
        public const string EnrolleeWallet = "EnrolleeWallet";
        public const string ProviderWallet = "ProviderWallet";

        public static bool IsValid(string? value)
        {
            return string.Equals(value, EnrolleeWallet, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, ProviderWallet, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string? value)
        {
            return string.Equals(value, ProviderWallet, StringComparison.OrdinalIgnoreCase)
                ? ProviderWallet
                : EnrolleeWallet;
        }

        public static string DisplayName(string? value)
        {
            return string.Equals(value, ProviderWallet, StringComparison.OrdinalIgnoreCase)
                ? "Provider Wallet"
                : "Enrollee Personal Wallet";
        }
    }
}
