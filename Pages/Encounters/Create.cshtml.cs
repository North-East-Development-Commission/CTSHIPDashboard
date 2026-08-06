using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.Pages.Encounters
{
    public class CreateModel : PageModel
    {
        [BindProperty]
        public EncounterViewModel Encounter { get; set; } = new();

        public List<SelectListItem> TypeOfVisitItems { get; set; } = new();
        public List<SelectListItem> PresentingComplaintsItems { get; set; } = new();
        public List<SelectListItem> DiagnosisItems { get; set; } = new();
        public Dictionary<string, List<SelectListItem>> ServicesProvidedItems { get; set; } = new();
        public List<SelectListItem> PatientOutcomeItems { get; set; } = new();
        public List<SelectListItem> SexItems { get; set; } = new();

        public void OnGet()
        {
            TypeOfVisitItems = EncounterLookups.TypesOfVisit
                .Select(x => new SelectListItem(x, x)).ToList();

            PresentingComplaintsItems = EncounterLookups.PresentingComplaints
                .Select(x => new SelectListItem(x, x)).ToList();

            DiagnosisItems = EncounterLookups.Diagnoses
                .Select(x => new SelectListItem(x, x)).ToList();

            foreach (var kv in EncounterLookups.ServicesProvided)
            {
                ServicesProvidedItems[kv.Key] = kv.Value
                    .Select(v => new SelectListItem(v, v)).ToList();
            }

            PatientOutcomeItems = EncounterLookups.PatientOutcomes
                .Select(x => new SelectListItem(x, x)).ToList();

            SexItems = EncounterLookups.SexOptions
                .Select(x => new SelectListItem(x, x)).ToList();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                OnGet(); // repopulate lists
                return Page();
            }

            // TODO: map Encounter to your domain Encounter entity and save to DB via ApplicationDbContext.
            // Example (pseudo):
            // var entity = new Models.Encounter { ... };
            // _context.Encounters.Add(entity);
            // await _context.SaveChangesAsync();

            TempData["Success"] = "Encounter recorded (UI demo). Implement persistence mapping in OnPost.";
            return RedirectToPage("./Create");
        }
    }
}