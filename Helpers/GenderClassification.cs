namespace CTSHIPDashboard.Helpers
{
    public static class GenderClassification
    {
        public static string? Normalize(string? gender) => gender?.Trim().ToUpperInvariant() switch
        {
            "M" or "MALE" => "Male",
            "F" or "FEMALE" => "Female",
            _ => null
        };

        public static bool IsMale(string? gender) => Normalize(gender) == "Male";

        public static bool IsFemale(string? gender) => Normalize(gender) == "Female";
    }
}
