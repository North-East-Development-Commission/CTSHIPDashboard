using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,CTSHIPAdmin,HMO")]
    public class MedicalHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MedicalHistoryController(ApplicationDbContext context) => _context = context;

        public IActionResult Create(int enrolleeId)
        {
            ViewBag.EnrolleeId = enrolleeId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(MedicalHistory history)
        {
            if (ModelState.IsValid)
            {
                history.RecordedBy = User.Identity?.Name ?? "Provider";
                _context.MedicalHistories.Add(history);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Medical history added.";
                return RedirectToAction("Details", "Enrollees", new { id = history.EnrolleeId });
            }
            return View(history);
        }
    }
}
