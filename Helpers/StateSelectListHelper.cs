using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Helpers
{
    public static class StateSelectListHelper
    {
        public static List<SelectListItem> NorthEastStates(string? selectedState = null)
        {
            return NorthEastLocationData.States
                .Select(state => new SelectListItem
                {
                    Value = state,
                    Text = state,
                    Selected = string.Equals(state, selectedState, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }

        public static List<SelectListItem> NorthEastStatesWithAll(
            string? selectedState = null,
            string allValue = "all",
            string allText = "All States")
        {
            List<SelectListItem> states = NorthEastStates(selectedState);
            states.Insert(0, new SelectListItem
            {
                Value = allValue,
                Text = allText,
                Selected = string.IsNullOrWhiteSpace(selectedState)
                    || string.Equals(selectedState, allValue, StringComparison.OrdinalIgnoreCase)
            });

            return states;
        }
    }
}
