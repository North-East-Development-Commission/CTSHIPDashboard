using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Monitoring,Reviewer,NHIA,IHSA,NEDCAdmin")]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AnalyticsHub> _hubContext;
        private readonly IAuditService _auditService;

        public ClaimsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<AnalyticsHub> hubContext,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _auditService = auditService;
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
               .WhereProviderCanUseClaims()
               .AsQueryable();

            query = await ScopeClaimsToCurrentUserAsync(query);

            ClaimMatrixViewModel matrix = ClaimMetricsService.Build(
                await query.Include(c => c.Queries).ToListAsync());
            PopulateClaimMatrixViewBag(matrix);

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
                query = string.Equals(status, "Queried", StringComparison.OrdinalIgnoreCase)
                    ? query.Where(c => c.Status == "Queried" || c.Status == "Query Raised")
                    : query.Where(c => c.Status == status);

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
                .Include(c => c.SupportingDocuments)
                .Include(c => c.Queries)
                .Include(c => c.AuditTrails)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();
            if (!ProviderClaimAccessHelper.CanUseClaims(claim.Provider)) return NotFound();
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
                    photoPath = EnrolleePhotoStorage.ResolvePhotoPath(enrollee.PhotoPath, enrollee.EnrollmentNumber),
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
                    photoPath = EnrolleePhotoStorage.ResolvePhotoPath(enrollee.PhotoPath, enrollee.EnrollmentNumber),
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
                .FirstOrDefaultAsync(c => c.Id == id && (c.Status == "Submitted" || c.Status == "Under Review"));

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
                ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync(
                    "Claim.Updated",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    existing.ClaimNumber,
                    AuditActor.Details(
                        $"Amount:NGN {existing.Amount:N2}",
                        $"Provider:{existing.ProviderId}",
                        $"Status:{existing.Status}"),
                    HttpContext.RequestAborted);
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
            var claim = await _context.Claims
                .Include(c => c.SupportingDocuments)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (claim != null)
            {
                _context.ClaimSupportingDocuments.RemoveRange(claim.SupportingDocuments);
                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();

                ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync(
                    "Claim.Deleted",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    claim.ClaimNumber,
                    AuditActor.Details(
                        $"Amount:NGN {claim.Amount:N2}",
                        $"Provider:{claim.ProviderId}",
                        $"Enrollee:{claim.EnrolleeId}"),
                    HttpContext.RequestAborted);

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
                .Include(c => c.SupportingDocuments)
                .FirstOrDefaultAsync(c => c.Id == id && (c.Status == "Submitted" || c.Status == "Under Review"));

            if (claim == null) return NotFound();
            if (!ProviderClaimAccessHelper.CanUseClaims(claim.Provider)) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();
            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Review(int id, string action, string notes, decimal? approvedAmount = null, string? deductionReason = null)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null || (claim.Status != "Submitted" && claim.Status != "Under Review")) return NotFound();
            if (!await _context.Claims.WhereProviderCanUseClaims().AnyAsync(x => x.Id == id)) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();

            var user = await _userManager.GetUserAsync(User);
            string actorName = user?.FullName ?? user?.Email ?? "Reviewer";

            if (action == "approve")
            {
                decimal validatedApprovedAmount = approvedAmount.GetValueOrDefault(claim.Amount);
                if (validatedApprovedAmount < 0)
                {
                    validatedApprovedAmount = 0;
                }

                claim.AmountApproved = validatedApprovedAmount;
                claim.DeductionAmount = Math.Max(claim.Amount - validatedApprovedAmount, 0m);
                claim.DeductionReason = string.IsNullOrWhiteSpace(deductionReason) ? null : deductionReason.Trim();
                claim.Status = validatedApprovedAmount < claim.Amount ? "Partially Approved" : "Approved";
                claim.ReviewedBy = actorName;
                claim.DateReviewed = DateTime.Now;
                claim.ReviewNotes = notes;
                claim.ApprovedBy = actorName;
                claim.DateApproved = DateTime.Now;
                claim.ApprovalNotes = notes;
            }
            else if (action == "reject")
            {
                claim.Status = "Rejected";
                claim.RejectedBy = actorName;
                claim.DateRejected = DateTime.Now;
                claim.RejectionReason = notes;
            }
            else if (action == "query")
            {
                claim.Status = "Queried";
                claim.ReturnedForClarificationAt = DateTime.UtcNow;
                claim.ReturnedForClarificationBy = actorName;
                claim.ClarificationNote = notes?.Trim();

                _context.ClaimQueries.Add(new ClaimQuery
                {
                    ClaimId = claim.Id,
                    QueryNumber = $"QRY-{DateTime.UtcNow:yyyyMMddHHmmss}-{claim.Id}",
                    Status = "Open",
                    QueryRaised = string.IsNullOrWhiteSpace(notes) ? "Clarification required before HMO certification." : notes.Trim(),
                    ResponsiblePerson = claim.SubmittedBy,
                    RaisedAt = DateTime.UtcNow,
                    RaisedByName = actorName
                });
            }

            _context.ClaimAuditTrails.Add(new ClaimAuditTrail
            {
                ClaimId = claim.Id,
                Action = action == "query" ? "HMO.Queried" : action == "reject" ? "HMO.Rejected" : "HMO.ReviewApproved",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = $"HMO review action '{action}' recorded. Notes: {notes}"
            });

            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
                action == "reject" ? "Claim.ReviewRejected" : "Claim.ReviewApproved",
                AuditActor.Format(user, User.Identity?.Name),
                claim.ClaimNumber,
                AuditActor.Details(
                    $"Status:{claim.Status}",
                    $"Amount:NGN {claim.Amount:N2}",
                    $"Notes:{notes}"),
                HttpContext.RequestAborted);
            TempData["Success"] = $"Claim {claim.ClaimNumber} has been {claim.Status}!";
            return RedirectToClaimsLanding();
        }

        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _context.Claims
                .Include(c => c.Enrollee)
                .Include(c => c.Provider)
                .Include(c => c.SupportingDocuments)
                .FirstOrDefaultAsync(c => c.Id == id && (c.Status == "Approved" || c.Status == "Partially Approved"));

            if (claim == null) return NotFound();
            if (!ProviderClaimAccessHelper.CanUseClaims(claim.Provider)) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();
            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Reviewer")]
        public async Task<IActionResult> Approve(int id, string action, string paymentRef = "", string? notes = null, decimal? amountPaid = null)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null || (claim.Status != "Approved" && claim.Status != "Partially Approved")) return NotFound();
            if (!await _context.Claims.WhereProviderCanUseClaims().AnyAsync(x => x.Id == id)) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();

            var user = await _userManager.GetUserAsync(User);

            if (action == "pay")
            {
                decimal payableAmount = claim.AmountApproved > 0 ? claim.AmountApproved : claim.Amount;
                claim.AmountPaid = Math.Max(amountPaid.GetValueOrDefault(payableAmount), 0m);
                claim.Status = claim.AmountPaid >= payableAmount ? "Paid" : "Partially Approved";
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

            _context.ClaimAuditTrails.Add(new ClaimAuditTrail
            {
                ClaimId = claim.Id,
                Action = action == "reject" ? "HMO.PaymentRejected" : "HMO.Paid",
                PerformedByName = user?.FullName ?? user?.Email ?? "Reviewer",
                PerformedAt = DateTime.UtcNow,
                Summary = action == "reject" ? claim.RejectionReason : $"Payment reference: {claim.PaymentReference}"
            });

            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
                action == "reject" ? "Claim.PaymentRejected" : "Claim.Paid",
                AuditActor.Format(user, User.Identity?.Name),
                claim.ClaimNumber,
                AuditActor.Details(
                    $"Status:{claim.Status}",
                    $"Amount:NGN {claim.Amount:N2}",
                    string.IsNullOrWhiteSpace(claim.PaymentReference) ? null : $"PaymentRef:{claim.PaymentReference}",
                    $"Notes:{notes}"),
                HttpContext.RequestAborted);
            TempData["Success"] = $"Claim {claim.ClaimNumber} is now {claim.Status}!";
            return RedirectToClaimsLanding();
        }


        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Reviewer,Monitoring,NHIA,IHSA,NEDCAdmin")]
        public async Task<IActionResult> SecondaryProviderClaimsReport(
            string search = "",
            string status = "All",
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Claim> baseQuery = _context.Claims
                .AsNoTracking()
                .Include(c => c.Enrollee!)
                    .ThenInclude(e => e.Hmo!)
                .Include(c => c.Provider!)
                .Include(c => c.Queries)
                .Where(c => c.Provider != null && c.Provider.Level == "Secondary");

            baseQuery = await ScopeClaimsToCurrentUserAsync(baseQuery);

            IQueryable<Claim> query = baseQuery;
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = $"%{search.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.ClaimNumber, term) ||
                    EF.Functions.Like(c.Enrollee!.FullName, term) ||
                    EF.Functions.Like(c.Enrollee!.EnrollmentNumber, term) ||
                    EF.Functions.Like(c.Provider!.Name, term));
            }

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = string.Equals(status, "Queried", StringComparison.OrdinalIgnoreCase)
                    ? query.Where(c => c.Status == "Queried" || c.Status == "Query Raised")
                    : query.Where(c => c.Status == status);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(c => c.DateSubmitted >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                DateTime exclusiveTo = toDate.Value.Date.AddDays(1);
                query = query.Where(c => c.DateSubmitted < exclusiveTo);
            }

            List<Claim> claims = await query
                .OrderByDescending(c => c.DateSubmitted)
                .Take(500)
                .ToListAsync(cancellationToken);

            DateTime today = DateTime.Today;
            List<Claim> closedClaims = claims
                .Where(c => c.DatePaid.HasValue || c.DateProcessed.HasValue || c.DateApproved.HasValue || c.DateRejected.HasValue)
                .ToList();

            var model = new SecondaryProviderClaimsReportViewModel
            {
                Search = search,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                TotalClaims = claims.Count,
                SubmittedClaims = claims.Count(c => c.Status == "Submitted"),
                ClaimsValidated = claims.Count(c => c.Status == "Approved" || c.Status == "Partially Approved" || c.Status == "Paid"),
                QueryClaims = claims.Count(c => c.Status == "Queried" || c.Status == "Query Raised" || c.Queries.Any(q => q.Status != "Closed")),
                ApprovedClaims = claims.Count(c => c.Status == "Approved"),
                PartiallyApprovedClaims = claims.Count(c => c.Status == "Partially Approved"),
                PaidClaims = claims.Count(c => c.Status == "Paid"),
                RejectedClaims = claims.Count(c => c.Status == "Rejected"),
                CertifiedClaims = claims.Count(c => c.HmoCertificationStatus == "Certified"),
                IhsaVerifiedClaims = claims.Count(c => c.IhsaVerificationStatus == "Verified"),
                TotalClaimAmount = claims.Sum(c => c.Amount),
                ApprovedClaimAmount = claims.Sum(c => c.AmountApproved > 0 ? c.AmountApproved : c.Amount),
                PaidClaimAmount = claims.Sum(c => c.AmountPaid),
                OutstandingClaimAmount = claims.Sum(c => Math.Max((c.AmountApproved > 0 ? c.AmountApproved : c.Amount) - c.AmountPaid, 0m)),
                AverageProcessingDays = closedClaims.Count > 0
                    ? Math.Round(closedClaims.Average(c => ((c.DatePaid ?? c.DateProcessed ?? c.DateApproved ?? c.DateRejected)!.Value.Date - c.DateSubmitted.Date).TotalDays), 1)
                    : 0,
                Claims = claims.Select(c => new SecondaryProviderClaimRowViewModel
                {
                    Id = c.Id,
                    ClaimNumber = c.ClaimNumber,
                    ReportingMonth = (c.DateOfService ?? c.DateSubmitted).ToString("MMM yyyy"),
                    EnrolleeName = c.Enrollee?.FullName ?? "N/A",
                    EnrollmentNumber = c.Enrollee?.EnrollmentNumber ?? "N/A",
                    ProviderName = c.Provider?.Name ?? "N/A",
                    HmoName = c.Enrollee?.Hmo?.Name ?? "N/A",
                    State = c.Provider?.State ?? c.Enrollee?.State ?? "N/A",
                    DateOfService = c.DateOfService,
                    Diagnosis = c.Diagnosis,
                    ServiceCategory = c.ServiceCategory ?? "N/A",
                    ReferralFacility = c.ReferralFacility ?? "N/A",
                    AuthorizationNumber = c.AuthorizationNumber ?? "N/A",
                    ServiceProcedure = c.ServiceProcedure ?? c.Treatment,
                    PaymentType = "Fee-for-Service",
                    ApprovedTariff = c.ApprovedTariff,
                    Amount = c.Amount,
                    AmountApproved = c.AmountApproved > 0 ? c.AmountApproved : c.Amount,
                    DeductionAmount = c.DeductionAmount,
                    DeductionReason = c.DeductionReason ?? string.Empty,
                    AdjustmentAmount = c.DeductionAmount > 0 ? c.DeductionAmount : Math.Max(c.Amount - (c.AmountApproved > 0 ? c.AmountApproved : c.Amount), 0m),
                    AdjustmentReason = c.DeductionReason ?? c.RejectionReason ?? string.Empty,
                    AmountPaid = c.AmountPaid,
                    OutstandingAmount = Math.Max((c.AmountApproved > 0 ? c.AmountApproved : c.Amount) - c.AmountPaid, 0m),
                    ApprovalDate = c.DateApproved,
                    PaymentDate = c.DatePaid,
                    PaymentReference = c.PaymentReference ?? string.Empty,
                    Status = c.Status,
                    ValidationStatus = c.Status == "Rejected"
                        ? "Rejected"
                        : c.Status == "Queried" || c.Status == "Query Raised" || c.Queries.Any(q => q.Status != "Closed")
                            ? "Queried"
                            : c.Status == "Approved" || c.Status == "Partially Approved" || c.Status == "Paid"
                                ? "Validated"
                                : "Pending",
                    PaymentStatus = c.Status == "Paid"
                        ? "Paid"
                        : c.Status == "Approved" || c.Status == "Partially Approved"
                            ? "Approved"
                            : "Pending",
                    HmoCertificationStatus = c.HmoCertificationStatus,
                    IhsaVerificationStatus = c.IhsaVerificationStatus,
                    OpenQueries = c.Queries.Count(q => q.Status != "Closed"),
                    ProcessingDays = (int)Math.Max(((c.DatePaid ?? c.DateProcessed ?? c.DateApproved ?? c.DateRejected ?? today).Date - c.DateSubmitted.Date).TotalDays, 0),
                    Remarks = c.ReviewNotes ?? c.ApprovalNotes ?? c.ClarificationNote ?? c.RejectionReason ?? string.Empty,
                    DateSubmitted = c.DateSubmitted
                }).ToList(),
                ProviderSummaries = claims
                    .GroupBy(c => new { Provider = c.Provider?.Name ?? "N/A", State = c.Provider?.State ?? "N/A" })
                    .Select(g => new SecondaryProviderClaimProviderSummaryViewModel
                    {
                        ProviderName = g.Key.Provider,
                        State = g.Key.State,
                        Claims = g.Count(),
                        Amount = g.Sum(c => c.Amount),
                        ApprovedAmount = g.Sum(c => c.AmountApproved > 0 ? c.AmountApproved : c.Amount),
                        PaidAmount = g.Sum(c => c.AmountPaid),
                        OutstandingAmount = g.Sum(c => Math.Max((c.AmountApproved > 0 ? c.AmountApproved : c.Amount) - c.AmountPaid, 0m)),
                        QueryClaims = g.Count(c => c.Status == "Queried" || c.Status == "Query Raised" || c.Queries.Any(q => q.Status != "Closed")),
                        PaidClaims = g.Count(c => c.Status == "Paid")
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList(),
                StatusSummaries = claims
                    .GroupBy(c => c.Status)
                    .Select(g => new SecondaryProviderClaimStatusSummaryViewModel
                    {
                        Status = g.Key,
                        Claims = g.Count(),
                        Amount = g.Sum(c => c.Amount)
                    })
                    .OrderByDescending(x => x.Claims)
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HMO,Reviewer")]
        public async Task<IActionResult> CloseClaimQuery(int queryId, string resolution, string? closureNote = null, CancellationToken cancellationToken = default)
        {
            ClaimQuery? query = await _context.ClaimQueries
                .Include(x => x.Claim)
                .FirstOrDefaultAsync(x => x.Id == queryId, cancellationToken);
            if (query?.Claim == null) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(query.Claim)) return Forbid();

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            string actorName = user?.FullName ?? user?.Email ?? "Reviewer";
            query.Resolution = resolution?.Trim();
            query.ClosureNote = closureNote?.Trim();
            query.ResolvedAt = DateTime.UtcNow;
            query.ResolvedByName = actorName;
            query.ClosedAt = DateTime.UtcNow;
            query.ClosedByName = actorName;
            query.Status = "Closed";

            if (!await _context.ClaimQueries.AnyAsync(x => x.ClaimId == query.ClaimId && x.Id != query.Id && x.Status != "Closed", cancellationToken))
            {
                query.Claim.Status = "Submitted";
            }

            _context.ClaimAuditTrails.Add(new ClaimAuditTrail
            {
                ClaimId = query.ClaimId,
                Action = "HMO.QueryClosed",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = $"Query {query.QueryNumber} closed. Resolution: {resolution}"
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Claim query closed with resolution and timestamp.";
            return RedirectToAction(nameof(Details), new { id = query.ClaimId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HMO,Reviewer")]
        public async Task<IActionResult> CertifyClaim(int id, string? note = null, CancellationToken cancellationToken = default)
        {
            Claim? claim = await _context.Claims.Include(c => c.Queries).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (claim == null) return NotFound();
            if (!await CanAccessHmoScopedClaimAsync(claim)) return Forbid();
            if (claim.Queries.Any(q => q.Status != "Closed"))
            {
                TempData["Error"] = "Close all claim queries before HMO certification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            string actorName = user?.FullName ?? user?.Email ?? "Reviewer";
            claim.HmoCertificationStatus = "Certified";
            claim.HmoCertifiedBy = actorName;
            claim.HmoCertifiedAt = DateTime.UtcNow;
            claim.HmoCertificationNote = note?.Trim();
            claim.IhsaVerificationStatus = "Ready for IHSA";

            _context.ClaimAuditTrails.Add(new ClaimAuditTrail
            {
                ClaimId = claim.Id,
                Action = "HMO.Certified",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = "HMO electronically certified the claim dataset without overwriting provider source data."
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Claim certified and moved to IHSA verification queue.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "IHSA,NEDCAdmin,NHIA,Monitoring,CTSHIPAdmin,Admin")]
        public async Task<IActionResult> VerifyClaimByIhsa(int id, string verificationStatus, string? note = null, CancellationToken cancellationToken = default)
        {
            if (verificationStatus is not "Verified" and not "Flagged")
            {
                TempData["Error"] = "Select a valid IHSA verification decision.";
                return RedirectToAction(nameof(Details), new { id });
            }

            Claim? claim = await _context.Claims.FindAsync(new object?[] { id }, cancellationToken);
            if (claim == null) return NotFound();
            if (claim.HmoCertificationStatus != "Certified")
            {
                TempData["Error"] = "HMO certification is required before IHSA verification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            string actorName = user?.FullName ?? user?.Email ?? User.Identity?.Name ?? "IHSA";
            claim.IhsaVerificationStatus = verificationStatus;
            claim.IhsaVerifiedBy = actorName;
            claim.IhsaVerifiedAt = DateTime.UtcNow;
            claim.IhsaVerificationNote = note?.Trim();

            _context.ClaimAuditTrails.Add(new ClaimAuditTrail
            {
                ClaimId = claim.Id,
                Action = verificationStatus == "Verified" ? "IHSA.Verified" : "IHSA.Flagged",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = note?.Trim()
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "IHSA verification decision saved.";
            return RedirectToAction(nameof(Details), new { id });
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

            var baseQuery = _context.Claims
                .Include(c => c.Enrollee!)
                    .ThenInclude(e => e.Hmo)
                .Include(c => c.Provider)
                .Where(c => c.HmoId == currentUser.HmoId.Value)
                .WhereProviderCanUseClaims();

            var query = baseQuery;

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
                query = string.Equals(status, "Queried", StringComparison.OrdinalIgnoreCase)
                    ? query.Where(c => c.Status == "Queried" || c.Status == "Query Raised")
                    : query.Where(c => c.Status == status);

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

            ClaimMatrixViewModel matrix = ClaimMetricsService.Build(
                await baseQuery.Include(c => c.Queries).ToListAsync());

            ViewBag.HmoName = hmoName;
            PopulateClaimMatrixViewBag(matrix);

            // FILTER VALUES
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page > 0 ? page : 1;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(claims);
        }

        private void PopulateClaimMatrixViewBag(ClaimMatrixViewModel matrix)
        {
            ViewBag.TotalClaims = matrix.TotalClaims;
            ViewBag.SubmittedClaims = matrix.SubmittedClaims;
            ViewBag.PendingClaims = matrix.SubmittedClaims;
            ViewBag.ClaimsValidated = matrix.ClaimsValidated;
            ViewBag.QueryClaims = matrix.QueryClaims;
            ViewBag.ApprovedClaims = matrix.ApprovedClaims;
            ViewBag.PaidClaims = matrix.PaidClaims;
            ViewBag.RejectedClaims = matrix.RejectedClaims;
            ViewBag.OutstandingClaims = matrix.OutstandingClaims;
            ViewBag.TotalAmount = matrix.TotalClaimAmount;
            ViewBag.AmountClaimed = matrix.TotalClaimAmount;
            ViewBag.AmountApproved = matrix.ApprovedClaimAmount;
            ViewBag.AmountPaid = matrix.PaidClaimAmount;
            ViewBag.OutstandingAmount = matrix.OutstandingClaimAmount;
            ViewBag.AverageProcessingDays = matrix.AverageProcessingDays;
        }

        private async Task<IQueryable<Claim>> ScopeClaimsToCurrentUserAsync(IQueryable<Claim> query)
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Admin")
                || User.IsInRole("Monitoring")
                || User.IsInRole("NHIA")
                || User.IsInRole("IHSA")
                || User.IsInRole("NEDCAdmin"))
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
                || User.IsInRole("Monitoring")
                || User.IsInRole("NHIA")
                || User.IsInRole("IHSA")
                || User.IsInRole("NEDCAdmin"))
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
                || User.IsInRole("Monitoring")
                || User.IsInRole("NHIA")
                || User.IsInRole("IHSA")
                || User.IsInRole("NEDCAdmin"))
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



