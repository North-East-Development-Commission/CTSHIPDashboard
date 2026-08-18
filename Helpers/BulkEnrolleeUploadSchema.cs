namespace CTSHIPDashboard.Helpers
{
    public sealed record BulkEnrolleeColumn(
        string Header,
        string Description,
        string Example,
        bool Required = true);

    public static class BulkEnrolleeUploadSchema
    {
        public static IReadOnlyList<BulkEnrolleeColumn> Columns { get; } =
            new List<BulkEnrolleeColumn>
            {
                new("FullName", "Enrollee's complete name. Required.", "Amina Musa"),
                new("Gender", "Required. Use M, F, Male, or Female.", "F"),
                new("DateOfBirth", "Required. Use dd/MM/yyyy.", "18/04/1992"),
                new("Phone", "Required. Keep as text so the leading zero is retained.", "08012345678"),
                new("NIN", "Required. Exactly 11 digits and unique.", "12345678901"),
                new("State", "Required. Adamawa, Bauchi, Borno, Gombe, Taraba, or Yobe.", "Borno"),
                new("LGA", "Required. Local Government Area.", "Maiduguri Metropolitan"),
                new("Ward", "Required. Enrollee's ward.", "Shehuri North"),
                new("Address", "Required. Residential address.", "12 Example Street, Maiduguri"),
                new("VulnerabilityCategory", "Optional. Use Pregnant Woman, PLWD, IDP, Others, or leave blank.", "IDP", false),
                new("OtherVulnerableCategory", "Optional. Required only when VulnerabilityCategory is Others.", "Conflict-affected person", false)
            };

        public static IReadOnlyList<string> RequiredHeaders { get; } =
            Columns.Where(column => column.Required).Select(column => column.Header).ToList();

        public static string NormalizeHeader(string? value) =>
            new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}

