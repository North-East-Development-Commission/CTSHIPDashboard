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
                new("EnrollmentNumber", "Required column. Enter the existing enrolment number if one has already been issued; leave the cell blank to generate a new one.", "CTH-2025-BN-100001"),
                new("FullName", "Enrollee's complete name. Required.", "Amina Musa"),
                new("Gender", "Required. Use M, F, Male, or Female.", "F"),
                new("DateOfBirth", "Required. Use dd/MM/yyyy.", "18/04/1992"),
                new("DateRegistered", "Optional. Any valid Excel date/time or typed date/time is accepted; leave blank to use the upload time.", "06/09/2026 14:30", false),
                new("Phone", "Optional. Keep as text so the leading zero is retained when provided.", "08012345678", false),
                new("NIN", "Optional. When provided, it must contain exactly 11 digits and be unique.", "12345678901", false),
                new("State", "Required. Adamawa, Bauchi, Borno, Gombe, Taraba, or Yobe.", "Borno"),
                new("LGA", "Required. Local Government Area.", "Maiduguri Metropolitan"),
                new("Ward", "Required. Enrollee's ward.", "Shehuri North"),
                new("Address", "Optional. Residential address; may be left blank.", "12 Example Street, Maiduguri", false),
                new("VulnerabilityCategory", "Optional. Enter any vulnerability category here. Pregnant women, Physically challenged, PLWD, and IDP set the corresponding flags. Older files may use OtherVulnerableCategory for the same field.", "Lactating mother", false)
            };

        public static IReadOnlyList<string> RequiredHeaders { get; } =
            Columns.Where(column => column.Required).Select(column => column.Header).ToList();

        public static string NormalizeHeader(string? value) =>
            new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

        // Both headers are accepted from older templates. Classify each value so
        // a known flag never causes additional free-text detail to be discarded.
        public static (bool IsPregnant, bool HasDisability, bool IsIdp, string? OtherCategory)
            MergeVulnerabilityCategories(params string?[] values)
        {
            bool pregnant = false, disability = false, idp = false;
            List<string> other = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string? value in values)
            {
                string text = value?.Trim() ?? string.Empty;
                string normalized = NormalizeHeader(text);
                if (normalized is "" or "NONE" or "NOTAPPLICABLE" or "NA" or "OTHER" or "OTHERS"
                    || !seen.Add(normalized)) continue;

                if (Services.VulnerabilityClassification.IsPregnant(false, text)) pregnant = true;
                else if (normalized is "PLWD" or "PWD" or "DISABILITY"
                    or "PERSONLIVINGWITHDISABILITY" or "PERSONLIVINGWITHDISABILITYPLWD"
                    or "PERSONSWITHDISABILITIES" or "PERSONWITHDISABILITY"
                    or "PERSONSLIVINGWITHDISABILITY" or "PHYSICALLYCHALLENGED") disability = true;
                else if (normalized is "IDP" or "IDPS" or "INTERNALLYDISPLACEDPERSON" or "INTERNALLYDISPLACEDPERSONS") idp = true;
                else other.Add(text);
            }
            return (pregnant, disability, idp, other.Count == 0 ? null : string.Join("; ", other));
        }
    }
}

