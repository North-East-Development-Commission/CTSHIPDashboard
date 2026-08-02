using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Helpers
{
    public static class EncounterReasonCatalog
    {
        public const string DefaultReason = "Acute illness";

        private static readonly string[] Reasons =
        {
            "Preventive services",
            "Acute illness",
            "Chronic disease management",
            "Maternal health",
            "Child health",
            "Reproductive health",
            "Injury/Emergency",
            "Follow-up care",
            "Administrative services",
            "Referral"
        };

        public static IReadOnlyList<string> All => Reasons;

        public static IReadOnlyDictionary<string, string> Examples { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Preventive services"] = "Routine check-up, immunization, screening, counselling, health education.",
                ["Acute illness"] = "Fever, cough, sore throat, headache, diarrhoea, vomiting, abdominal pain.",
                ["Chronic disease management"] = "Hypertension, diabetes, asthma, HIV, tuberculosis, epilepsy, mental health follow-up.",
                ["Maternal health"] = "Antenatal care, postnatal care, labour and delivery, pregnancy-related complaint.",
                ["Child health"] = "Child fever, diarrhoea, cough, growth monitoring, nutrition assessment, neonatal illness.",
                ["Reproductive health"] = "Family planning, infertility consultation, STI symptoms, pregnancy test.",
                ["Injury/Emergency"] = "Injury, trauma, emergency presentation, difficulty breathing.",
                ["Follow-up care"] = "Review visit, laboratory result review, prescription refill, drug collection.",
                ["Administrative services"] = "Medical certificate request, laboratory test request, home visit follow-up.",
                ["Referral"] = "Referral consultation or referral-related encounter."
            };

        public static bool IsValid(string? reason)
        {
            return !string.IsNullOrWhiteSpace(reason)
                && Reasons.Contains(reason.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static List<SelectListItem> BuildSelectList(string? selectedReason)
        {
            return Reasons
                .Select(reason => new SelectListItem
                {
                    Value = reason,
                    Text = reason,
                    Selected = string.Equals(reason, selectedReason, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }
    }
}
