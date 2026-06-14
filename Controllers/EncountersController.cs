using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,Admin")]
    public class EncountersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AnalyticsHub> _hubContext;

        public EncountersController(ApplicationDbContext context, IHubContext<AnalyticsHub> hubContext, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        // INDEX — FULL LIST WITH SEARCH & FILTER
        public async Task<IActionResult> Index(string search = "", string status = "All", int page = 1, int pageSize = 10)
        {
            var query = _context.Encounters
                .Include(e => e.Enrollee)
                .Include(e => e.Provider)
                .Include(e => e.Claim)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(e =>
                    e.Enrollee!.FullName.Contains(s) ||
                    e.Enrollee!.EnrollmentNumber.Contains(s) ||
                    e.EncounterNumber.Contains(s));
            }

            if (status != "All")
                query = query.Where(e => e.Status == status);

            var totalItems = await query.CountAsync();
            var model = await query
                .OrderByDescending(e => e.VisitDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            //ViewBag.CurrentPage = page;
            ViewBag.CurrentPage = page <= 0 ? 1 : page; // ALWAYS START FROM 1!
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(model);
        }

        // DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var encounter = await _context.Encounters
                .Include(e => e.Enrollee).ThenInclude(e => e!.Hmo)
                .Include(e => e.Provider)
                .Include(e => e.Claim)
                .FirstOrDefaultAsync(e => e.Id == id);
            var currentUser = await _userManager.GetUserAsync(User);
            encounter.AttendedBy = currentUser?.Email ?? "Unknown User";
            if (encounter == null) return NotFound();
            return View(encounter);
        }

        // CREATE GET
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        // CREATE POST — CLEAN & PERFECT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Encounter encounter)
        {
            if (ModelState.IsValid)
            {
                // Generate Encounter Number
                var lastEncounter = await _context.Encounters
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                int nextId = (lastEncounter?.Id ?? 0) + 1;
                string year = DateTime.Now.ToString("yyyy");
                encounter.EncounterNumber = $"ECN-{year}-{nextId:D6}";

                // SAFELY GET CURRENT USER — NO MORE NULL ERROR!
                var currentUser = await _userManager.GetUserAsync(User);
                encounter.AttendedBy = currentUser?.Email ?? "Unknown User";

                // OR use FullName if you have it:
                // encounter.AttendedBy = currentUser?.FullName ?? currentUser?.Email ?? "Unknown";

                encounter.Status = "Completed";
                encounter.IsBilled = false;

                _context.Encounters.Add(encounter);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Encounter {encounter.EncounterNumber} recorded by {encounter.AttendedBy}!";
                if (User.IsInRole("Provider"))
                {
                    return RedirectToAction("MyEncounters", "Providers");
                }
                else if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Enrollees");
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns();
            return View(encounter);
        }

        // EDIT GET — CLEAN
        public async Task<IActionResult> Edit(int id)
        {
            var encounter = await _context.Encounters.FindAsync(id);

            if (encounter == null) return NotFound();

            await PopulateDropdowns();
            return View(encounter);
        }

        // EDIT POST — CLEAN & SAFE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Encounter encounter)
        {
            if (id != encounter.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(encounter);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Encounter {encounter.EncounterNumber} updated successfully!";
                  
                   return RedirectToAction("Index", "Encounters");
                    
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Failed to update encounter. Please try again.";
                }
            }

            // ONLY CALL ONCE
            await PopulateDropdowns();
            return View(encounter);
        }

        // DELETE GET
        public async Task<IActionResult> Delete(int id)
        {
            var encounter = await _context.Encounters
                .Include(e => e.Enrollee)
                .Include(e => e.Provider)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (encounter == null) return NotFound();
            return View(encounter);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var encounter = await _context.Encounters.FindAsync(id);
            if (encounter != null)
            {
                _context.Encounters.Remove(encounter);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Encounter {encounter.EncounterNumber} deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // SEARCH ENROLLEE
        [HttpGet]
        public async Task<IActionResult> SearchEnrollee(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new { success = false });

            var enrollee = await _context.Enrollees
                .Include(e => e.Hmo)
                .FirstOrDefaultAsync(e => e.EnrollmentNumber.ToUpper() == q.Trim().ToUpper());

            if (enrollee == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                enrollee = new
                {
                    id = enrollee.Id,
                    fullName = enrollee.FullName,
                    enrollmentNumber = enrollee.EnrollmentNumber,
                    photoPath = enrollee.PhotoPath ?? "/img/icon-192.png",
                    hmoName = enrollee.Hmo?.Name ?? "Not Assigned",
                    state = enrollee.State
                }
            });
        }

        // CREATE CLAIM FROM ENCOUNTER — 100% SAFE & ACCURATE
        [Authorize(Roles = "Provider,Admin,HMO")]
        public async Task<IActionResult> CreateClaim(int id)
        {
            var encounter = await _context.Encounters
                .Include(e => e.Enrollee!)
                    .ThenInclude(e => e.Hmo!)
                .Include(e => e.Provider!)
                .FirstOrDefaultAsync(e => e.Id == id && e.ClaimId == null);

            // ENCOUNTER NOT FOUND OR ALREADY CLAIMED
            if (encounter == null)
            {
                TempData["Error"] = "Encounter not found or already has a claim.";
                return RedirectToAction("Index", "Encounters");
            }

            // ENROLLEE HAS NO HMO — BLOCK CLAIM
            if (encounter.Enrollee?.Hmo == null)
            {
                TempData["Error"] = $"Cannot create claim: {encounter.Enrollee?.FullName} has no HMO assigned.";
                return RedirectToAction("Details", "Encounters", new { id });
            }

            // CREATE CLAIM WITH CORRECT HMO
            var claim = new Claim
            {
                ClaimNumber = "CLM-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                EnrolleeId = encounter.EnrolleeId,
                ProviderId = encounter.ProviderId,
                HmoId = encounter.Enrollee.HmoId,                    // CORRECT HMO!
                Amount = encounter.TotalAmount,
                Diagnosis = encounter.Diagnosis ?? encounter.ChiefComplaint ?? "Clinical encounter",
                Treatment = encounter.TreatmentGiven ?? "Medical consultation and care",
                DateSubmitted = DateTime.Now,
                Status = "Submitted",
                SubmittedBy = User.Identity?.Name ?? "Provider"
            };

            _context.Claims.Add(claim);
            await _context.SaveChangesAsync();

            // UPDATE ENCOUNTER
            encounter.ClaimId = claim.Id;
            encounter.Status = "Claimed";
            await _context.SaveChangesAsync();

            // REAL-TIME NOTIFICATION
            await _hubContext.Clients.All.SendAsync("ClaimSubmitted", new
            {
                claim.Id,
                claim.ClaimNumber,
                EnrolleeName = encounter.Enrollee.FullName,
                HmoName = encounter.Enrollee.Hmo.Name,
                ProviderName = encounter.Provider.Name,
                Amount = claim.Amount,
                Status = "Submitted"
            });

            TempData["Success"] = $"Claim {claim.ClaimNumber} successfully created for {encounter.Enrollee.Hmo.Name}!";
            return RedirectToAction("Index", "Claims");
        }

        private async Task PopulateDropdowns()
        {
            var providers = await _context.Providers
               .Where(p => p.IsActive)
               .OrderBy(p => p.Name)
               .ToListAsync();

            ViewBag.Providers = providers
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.State}"
                })
                .ToList();

            ViewBag.Statuses = new List<SelectListItem>
            {
                new() { Value = "Completed", Text = "Completed" },
                new() { Value = "Pending", Text = "Pending" },
                new() { Value = "Cancelled", Text = "Cancelled" },
                new() { Value = "Referred", Text = "Referred" },
                new() { Value = "Claimed", Text = "Claimed" }
            };
        }
    }
}