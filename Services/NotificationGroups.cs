namespace CTSHIPDashboard.Services;

public static class NotificationGroups
{
    public static string Role(string role) => "role:" + Normalize(role);

    public static string User(string userId) => "user:" + Normalize(userId);

    public static string Hmo(int hmoId) => $"hmo:{hmoId}";

    public static string HmoCode(string hmoCode) => "hmo-code:" + Normalize(hmoCode);

    public static string Provider(int providerId) => $"provider:{providerId}";

    public static string ReferralHospital(Guid hospitalId) => $"referral-hospital:{hospitalId:N}";

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
