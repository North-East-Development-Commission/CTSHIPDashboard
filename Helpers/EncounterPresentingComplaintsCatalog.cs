using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace CTSHIPDashboard.Helpers
{
    public static class EncounterPresentingComplaintsCatalog
    {
        public static readonly List<string> All = new()
        {
            "Fever",
            "Cough",
            "Difficulty breathing",
            "Diarrhea",
            "Vomiting",
            "Headache",
            "Abdominal pain",
            "Hypertension review",
            "Diabetes review",
            "Antenatal Care (ANC)",
            "Postnatal Care (PNC)",
            "Family Planning",
            "Immunization",
            "Child Welfare",
            "Nutrition",
            "Injury",
            "Skin disease",
            "Eye problem",
            "Mental health",
            "Other"
        };

        public static List<SelectListItem> BuildSelectList(IEnumerable<string>? selected = null)
        {
            var set = new HashSet<string>(selected ?? new string[0], System.StringComparer.OrdinalIgnoreCase);
            var list = new List<SelectListItem>();
            foreach (var item in All)
            {
                list.Add(new SelectListItem
                {
                    Value = item,
                    Text = item,
                    Selected = set.Contains(item)
                });
            }
            return list;
        }
    }
}
