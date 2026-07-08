using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Monitoring,Reviewer")]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AnalyticsHub> _hubContext;

        public ClaimsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<AnalyticsHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // INDEX — ALL CLAIMS WITH FILTERS & SEARCH
        public async Task<IActionResult> Index(string status = "All", 
            string search = "", 
            int page = 1, 
            int pageSize = 20)
        {
            ViewBag.Status = status;
            ViewBag.Search = search;

            var query = _context.Claims
               .Include(c => c.Enrollee!)
               .ThenInclude(e => e.Hmo!)
               .Include(c => c.Provider!)
               .AsQueryable();

            query = await ScopeClaimsToCurrentUserAsync(query);

            ViewBag.TotalClaims = await query.CountAsync();
            ViewBag.PendingClaims = await query.CountAsync(c => c.Status == "Submitted" || c.Status == "Approved");
            ViewBag.PaidClaims = await query.CountAsync(c => c.Status == "Paid");
            ViewBag.RejectedClaims = await query.CountAsync(c => c.Status == "Rejected");

            // SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c =>
                    c.ClaimNumber.Contains(s) ||
                    c.Enrollee!.FullName.Contains(s) ||
                    c.Enrollee!.State.Contains(s) ||
                    c.Enrollee!.EnrollmentNumber.Contains(s));
            }

            // FILTER
            if (status != "All")
                query = query.Where(c => c.Status == status);

            // PAGINATION
            var total = await query.CountAsync();
            var model = await query
                .OrderByDescending(c => c.DateSubmitted)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling(total / 20.0);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(model);
        }

        // DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee).ThenInclude(e => e!.Hmo)
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();
            if (!await CanAccessClaimAsync(claim)) return Forbid();
            return View(claim);
        }

        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
        public IActionResult Create()
        {
            TempData["Error"] = "Claims must be created by providers from completed encounters.";
            return RedirectToClaimsLanding();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
        public IActionResult Create(Claim claim)
        {
            TempData["Error"] = "Claims must be created by providers from completed encounters.";
            return RedirectToClaimsLanding();
        }

        // SEARCH ENROLLEE BY ENROLLMENT NUMBER (AJAX)
        // RENAME THIS ACTION TO:
       
        [HttpGet]
        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
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

        [HttpGet]
        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
        public async Task<IActionResult> SearchByNumber(string q)
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

        // EDIT CLAIM (Only if status is Submitted)
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee)
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == "Submitted");

            if (claim == null)
            {
                TempData["Error"] = "Claim not found or cannot be edited.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Providers = await _context.Providers
                .Where(p => p.IsActive)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.State}"
                })
                .ToListAsync();

            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> Edit(int id, Claim claim)
        {
            if (id != claim.Id) return NotFound();

            var existing = await _context.Claims.FindAsync(id);
            if (existing == null || existing.Status != "Submitted")
            {
                TempData["Error"] = "Claim cannot be edited.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                existing.Diagnosis = claim.Diagnosis;
                existing.Treatment = claim.Treatment;
                existing.Amount = claim.Amount;
                existing.ProviderId = claim.ProviderId;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Claim updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Providers = await _context.Providers
                .Where(p => p.IsActive)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.State}"
                })
                .OrderBy(p => p.Text)
                .ToListAsync();

            return View(claim);
        }

        // DELETE (Admin/CTSHIPAdmin only)
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee)
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();
            return View(claim);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim != null)
            {
                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ClaimDeleted", id);
                TempData["Success"] = $"Claim {claim.ClaimNumber} deleted permanently.";
            }
            return RedirectToAction(nameof(Index));
        }

       // REVIEW CLAIM(HMO reviewer validation)
        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Review(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee).ThenInclude(e => e!.Hmo)
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == "Submitted");

            if (claim == null) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();
            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Review(int id, string action, string notes)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null || claim.Status != "Submitted") return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();

            var user = await _userManager.GetUserAsync(User);

            if (action == "approve")
            {
                claim.Status = "Approved";
                claim.ReviewedBy = user?.FullName ?? user?.Email ?? "Reviewer";
                claim.DateReviewed = DateTime.Now;
                claim.ReviewNotes = notes;
            }
            else if (action == "reject")
            {
                claim.Status = "Rejected";
                claim.RejectedBy = user?.FullName ?? user?.Email ?? "Reviewer";
                claim.DateRejected = DateTime.Now;
                claim.RejectionReason = notes;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Claim {claim.ClaimNumber} has been {claim.Status}!";
            return RedirectToClaimsLanding();
        }

        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee)
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == "Approved");

            if (claim == null) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();
            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Approve(int id, string action, string paymentRef = "", string? notes = null)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null || claim.Status != "Approved") return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();

            var user = await _userManager.GetUserAsync(User);

            if (action == "pay")
            {
                claim.Status = "Paid";
                claim.PaidBy = user?.FullName ?? user?.Email ?? "Reviewer";
                claim.DatePaid = DateTime.Now;
                claim.DateProcessed = DateTime.Now;
                claim.ApprovalNotes = notes?.Trim();
                claim.PaymentReference = string.IsNullOrEmpty(paymentRef)
                    ? "HMO-PAY-" + DateTime.Now.ToString("yyyyMMddHHmmss")
                    : paymentRef;
            }
            else if (action == "reject")
            {
                claim.Status = "Rejected";
                claim.RejectedBy = user?.FullName ?? user?.Email ?? "Reviewer";
                claim.DateRejected = DateTime.Now;
                claim.DateProcessed = DateTime.Now;
                claim.RejectionReason = string.IsNullOrWhiteSpace(notes)
                    ? "Rejected during payment validation"
                    : notes.Trim();
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Claim {claim.ClaimNumber} is now {claim.Status}!";
            return RedirectToClaimsLanding();
        }


        [Authorize(Roles = "HMO,Reviewer")]
        public async Task<IActionResult> Dashboard(
            string search = "",
            string status = "All",
            int page = 1,
            int pageSize = 10)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.HmoId == null)
            {
                TempData["Error"] = "Your account is not linked to any HMO.";
                return RedirectToAction("Index", "Home");
            }

            var query = _context.Claims
                .Include(c => c.Enrollee!)
                    .ThenInclude(e => e.Hmo)
                .Include(c => c.Provider)
                .Where(c => c.HmoId == currentUser.HmoId.Value);

            // SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = $"%{search.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.ClaimNumber, s) ||
                    EF.Functions.Like(c.Enrollee!.FullName, s) ||
                    EF.Functions.Like(c.Enrollee!.EnrollmentNumber, s));
            }

            // FILTER BY STATUS
            if (status != "All")
                query = query.Where(c => c.Status == status);

            // TOTAL COUNT
            var totalItems = await query.CountAsync();

            // PAGINATION
            var claims = await query
                .OrderByDescending(c => c.DateSubmitted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // STATS
            var hmoName = claims.FirstOrDefault()?.Enrollee?.Hmo?.Name ?? "Your HMO";

            ViewBag.HmoName = hmoName;
            ViewBag.TotalClaims = totalItems;
            ViewBag.PendingClaims = await query.CountAsync(c => c.HmoId == currentUser.HmoId && c.Status == "Submitted");
            ViewBag.ApprovedClaims = await _context.Claims.CountAsync(c => c.HmoId == currentUser.HmoId && c.Status == "Approved");
            ViewBag.PaidClaims = await _context.Claims.CountAsync(c => c.HmoId == currentUser.HmoId && c.Status == "Paid");
            ViewBag.RejectedClaims = await _context.Claims.CountAsync(c => c.HmoId == currentUser.HmoId && c.Status == "Rejected");
            ViewBag.TotalAmount = await _context.Claims
                .Where(c => c.HmoId == currentUser.HmoId)
                .SumAsync(c => c.Amount);

            // FILTER VALUES
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page > 0 ? page : 1;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(claims);
        }

        private async Task<IQueryable<Claim>> ScopeClaimsToCurrentUserAsync(IQueryable<Claim> query)
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Admin")
                || User.IsInRole("Monitoring"))
            {
                return query;
            }

            if (IsHmoScopedClaimsUser())
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (!currentUser?.HmoId.HasValue ?? true)
                {
                    return query.Where(claim => false);
                }

                int hmoId = currentUser!.HmoId!.Value;
                query = query.Where(claim => claim.HmoId == hmoId);
            }

            return query;
        }

        private async Task<bool> CanAccessClaimAsync(Claim claim)
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Admin")
                || User.IsInRole("Monitoring"))
            {
                return true;
            }

            if (IsHmoScopedClaimsUser())
            {
                var currentUser = await _userManager.GetUserAsync(User);
                return currentUser?.HmoId.HasValue == true
                    && claim.HmoId == currentUser.HmoId.Value;
            }

            return false;
        }

        private async Task<bool> CanAccessHmoScopedClaimAsync(Claim claim)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            return currentUser?.HmoId.HasValue == true
                && claim.HmoId == currentUser.HmoId.Value;
        }

        private IActionResult RedirectToClaimsLanding()
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Admin")
                || User.IsInRole("Monitoring"))
            {
                return RedirectToAction(nameof(Index));
            }

            if (IsHmoScopedClaimsUser())
            {
                return RedirectToAction(nameof(Dashboard));
            }

            return RedirectToAction(nameof(Index));
        }

        private bool IsHmoScopedClaimsUser()
        {
            return User.IsInRole("HMO") || User.IsInRole("Reviewer");
        }
    }
}
