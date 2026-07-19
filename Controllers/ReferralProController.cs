using System.Data;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AppClaim = CTSHIPDashboard.Models.Claim;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "ReferralPro,CTSHIPAdmin")]
[Route("ReferralPro/Referrals")]
public class ReferralProController : Controller
{
    private const long MaxClaimSupportFileBytes = 10 * 1024 * 1024;
    private const int MaxSupportingDocumentCount = 10;

    private static readonly HashSet<string> AllowedClaimSupportFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".doc",
        ".docx"
    };

    private sealed class ReferralProviderClaimMetrics
    {
        public int TotalClaims { get; set; }

        public int PendingClaims { get; set; }

        public int ApprovedClaims { get; set; }

        public int PaidClaims { get; set; }

        public int RejectedClaims { get; set; }

        public decimal TotalClaimValue { get; set; }

        public decimal PaidClaimValue { get; set; }
    }

    private sealed class ReferralProviderComplaintMetrics
    {
        public int TotalComplaints { get; set; }

        public int OpenComplaints { get; set; }

        public int EscalatedComplaints { get; set; }

        public int ResolvedComplaints { get; set; }
    }

    private readonly ApplicationDbContext _context;
    private readonly IReferralService _referralService;
    private readonly IAppNotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ReferralProController(
        ApplicationDbContext context,
        IReferralService referralService,
        IAppNotificationService notificationService,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _referralService = referralService;
        _notificationService = notificationService;
        _userManager = userManager;
        _environment = environment;
    }

    [HttpGet("/ReferralPro/Dashboard")]
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken = default)
    {
        ReferredHospital? currentHospital = await GetCurrentReferralHospitalAsync(cancellationToken);
        if (!User.IsInRole("CTSHIPAdmin") && currentHospital == null)
        {
            return View(new ReferralProviderDashboardViewModel
            {
                FacilityName = "Referral Provider",
                IsLinkedToReferralHospital = false
            });
        }

        IQueryable<Referral> query = BuildReferralProQuery(currentHospital);
        DateTime monthStart = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        int totalReferrals = await query.CountAsync(cancellationToken);
        int readyToReceive = await query.CountAsync(x =>
            x.Status == ReferralStatus.Verified || x.Status == ReferralStatus.Audited,
            cancellationToken);
        int received = await query.CountAsync(x => x.Status == ReferralStatus.Received, cancellationToken);
        int completed = await query.CountAsync(x => x.Status == ReferralStatus.Closed, cancellationToken);
        int expiredCodes = await query.CountAsync(x =>
            (x.Status == ReferralStatus.Verified || x.Status == ReferralStatus.Audited) &&
            x.ReferralVerificationCodeExpiresAt.HasValue &&
            x.ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow,
            cancellationToken);
        int thisMonth = await query.CountAsync(x => x.CreatedAt >= monthStart, cancellationToken);
        ReferralProviderClaimMetrics claimMetrics = await GetReferralClaimMetricsAsync(currentHospital, cancellationToken);
        ReferralProviderComplaintMetrics complaintMetrics = await GetReferralComplaintMetricsAsync(currentHospital, cancellationToken);

        List<ReferralProviderDashboardAlertViewModel> alerts = await query
            .Where(x => x.Status == ReferralStatus.Verified || x.Status == ReferralStatus.Audited)
            .OrderBy(x =>
                x.ReferralVerificationCodeExpiresAt.HasValue &&
                x.ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow
                    ? 0
                    : 1)
            .ThenByDescending(x => x.VerifiedAt ?? x.AuditedAt ?? x.SubmittedToHmoAt ?? x.CreatedAt)
            .Take(5)
            .Select(x => new ReferralProviderDashboardAlertViewModel
            {
                ReferralId = x.Id,
                Title = x.ReferralVerificationCodeExpiresAt.HasValue &&
                    x.ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow
                        ? "Referral code expired"
                        : "Referral ready for code verification",
                Message = x.EnrolleeFullName + " from " + x.FromProviderName,
                Icon = x.ReferralVerificationCodeExpiresAt.HasValue &&
                    x.ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow
                        ? "exclamation-triangle"
                        : "shield-check",
                CssClass = x.ReferralVerificationCodeExpiresAt.HasValue &&
                    x.ReferralVerificationCodeExpiresAt.Value <= DateTime.UtcNow
                        ? "alert-warning"
                        : "alert-success",
                AlertAt = x.VerifiedAt ?? x.AuditedAt ?? x.SubmittedToHmoAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        List<ReferralProviderDashboardReferralViewModel> recentReferrals = await query
            .OrderByDescending(x => x.VerifiedAt ?? x.AuditedAt ?? x.SubmittedToHmoAt ?? x.CreatedAt)
            .Take(8)
            .Select(x => new ReferralProviderDashboardReferralViewModel
            {
                Id = x.Id,
                EnrolleeNumber = x.EnrolleeNumber,
                EnrolleeFullName = x.EnrolleeFullName,
                FromProviderName = x.FromProviderName,
                HmoName = x.HmoName,
                Diagnosis = x.Diagnosis,
                Priority = x.Priority,
                Status = x.Status,
                ActivityAt = x.VerifiedAt ?? x.AuditedAt ?? x.SubmittedToHmoAt ?? x.CreatedAt,
                ReferralVerificationCodeExpiresAt = x.ReferralVerificationCodeExpiresAt
            })
            .ToListAsync(cancellationToken);

        return View(new ReferralProviderDashboardViewModel
        {
            FacilityName = currentHospital?.Name ?? "All Referral Providers",
            FacilityState = currentHospital?.State,
            FacilityLga = currentHospital?.Lga,
            TotalReferrals = totalReferrals,
            ReadyToReceive = readyToReceive,
            Received = received,
            Completed = completed,
            ExpiredCodes = expiredCodes,
            ThisMonth = thisMonth,
            SubmittedClaimValue = claimMetrics.TotalClaimValue,
            TotalClaims = claimMetrics.TotalClaims,
            PendingClaims = claimMetrics.PendingClaims,
            ApprovedClaims = claimMetrics.ApprovedClaims,
            PaidClaims = claimMetrics.PaidClaims,
            RejectedClaims = claimMetrics.RejectedClaims,
            PaidClaimValue = claimMetrics.PaidClaimValue,
            TotalComplaints = complaintMetrics.TotalComplaints,
            OpenComplaints = complaintMetrics.OpenComplaints,
            EscalatedComplaints = complaintMetrics.EscalatedComplaints,
            ResolvedComplaints = complaintMetrics.ResolvedComplaints,
            Alerts = alerts,
            RecentReferrals = recentReferrals
        });
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string status = "All",
        CancellationToken cancellationToken = default)
    {
        ReferredHospital? currentHospital = await GetCurrentReferralHospitalAsync(cancellationToken);
        if (!User.IsInRole("CTSHIPAdmin") && currentHospital == null)
        {
            TempData["Error"] = "Your ReferralPro account is not linked to an active referral hospital. Match the user email with the referral hospital email.";
            ViewBag.Search = search;
            ViewBag.Status = status;
            SetReferralCounts(0, 0, 0);
            return View(new List<ReferralIndexViewModel>());
        }

        IQueryable<Referral> query = BuildReferralProQuery(currentHospital);

        IQueryable<Referral> countQuery = query;

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x =>
                x.EnrolleeNumber.Contains(term) ||
                x.EnrolleeFullName.Contains(term) ||
                x.FromProviderName.Contains(term) ||
                x.Diagnosis.Contains(term) ||
                (x.HmoName != null && x.HmoName.Contains(term)) ||
                (x.ReferredHospital != null && x.ReferredHospital.Name.Contains(term)));
        }

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse(status, ignoreCase: true, out ReferralStatus selectedStatus))
        {
            query = query.Where(x => x.Status == selectedStatus);
        }

        SetReferralCounts(
            await countQuery.CountAsync(x => x.Status == ReferralStatus.Verified || x.Status == ReferralStatus.Audited, cancellationToken),
            await countQuery.CountAsync(x => x.Status == ReferralStatus.Received, cancellationToken),
            await countQuery.CountAsync(x => x.Status == ReferralStatus.Closed, cancellationToken));

        List<ReferralIndexViewModel> referrals = await query
            .OrderByDescending(x => x.VerifiedAt ?? x.AuditedAt ?? x.SubmittedToHmoAt ?? x.CreatedAt)
            .Select(x => new ReferralIndexViewModel
            {
                Id = x.Id,
                EnrolleeNumber = x.EnrolleeNumber,
                EnrolleeFullName = x.EnrolleeFullName,
                FromProviderName = x.FromProviderName,
                ReferredHospitalName = x.ReferredHospital == null ? string.Empty : x.ReferredHospital.Name,
                HmoName = x.HmoName,
                Diagnosis = x.Diagnosis,
                Priority = x.Priority,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SubmittedToHmoAt = x.SubmittedToHmoAt,
                VerifiedAt = x.VerifiedAt,
                AuditedAt = x.AuditedAt,
                ReferralVerificationCodeExpiresAt = x.ReferralVerificationCodeExpiresAt,
                ReferralVerificationCodeVerifiedAt = x.ReferralVerificationCodeVerifiedAt
            })
            .ToListAsync(cancellationToken);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CurrentHospitalName = currentHospital?.Name;
        return View(referrals);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        Referral? referral = await GetReferralWithDetailsAsync(id, cancellationToken);
        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        if (!User.IsInRole("CTSHIPAdmin") && RequiresReferralCodeVerification(referral))
        {
            TempData["Error"] = "Verify the referral code presented by the enrollee before viewing full referral details.";
            return RedirectToAction(nameof(VerifyCode), new { id });
        }

        ViewBag.EnrolleeId = await _context.Enrollees
            .AsNoTracking()
            .Where(x => x.EnrollmentNumber == referral.EnrolleeNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return View(MapReferralDetails(referral));
    }

    [HttpPost("Receive/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken = default)
    {
        Referral? referral = await GetReferralWithDetailsAsync(id, cancellationToken);
        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        if (referral.Status == ReferralStatus.Received)
        {
            TempData["Success"] = "Referral already received. You can proceed with the referral encounter.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (referral.Status == ReferralStatus.Closed)
        {
            TempData["Error"] = "This referral already has a submitted encounter and claim.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (referral.Status != ReferralStatus.Verified && referral.Status != ReferralStatus.Audited)
        {
            TempData["Error"] = "Only HMO-verified referrals can be received by the referred hospital.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Error"] = "Enter the HMO-issued referral verification code before receiving this referral.";
        return RedirectToAction(nameof(VerifyCode), new { id });
    }

    [HttpGet("VerifyCode")]
    [HttpGet("VerifyCode/{id:guid}")]
    public async Task<IActionResult> VerifyCode(Guid? id, CancellationToken cancellationToken = default)
    {
        ReferralCodeVerificationViewModel model = new()
        {
            ReferralId = id
        };

        if (!await PopulateCodeVerificationModelAsync(model, cancellationToken))
        {
            TempData["Error"] = "Your ReferralPro account is not linked to an active referral hospital.";
            return RedirectToAction(nameof(Index));
        }

        if (id.HasValue)
        {
            Referral? referral = await GetReferralWithDetailsAsync(id.Value, cancellationToken);
            if (referral == null)
            {
                return NotFound();
            }

            if (!await CanAccessReferralAsync(referral, cancellationToken))
            {
                return Forbid();
            }

            if (referral.Status == ReferralStatus.Received)
            {
                TempData["Success"] = "Referral code has already been verified.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (referral.Status == ReferralStatus.Closed)
            {
                TempData["Error"] = "This referral has already been closed.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        return View(model);
    }

    [HttpPost("VerifyCode")]
    [HttpPost("VerifyCode/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyCode(
        Guid? id,
        ReferralCodeVerificationViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (id.HasValue)
        {
            if (model.ReferralId.HasValue && model.ReferralId.Value != id.Value)
            {
                return BadRequest();
            }

            model.ReferralId = id.Value;
        }

        Guid? hospitalId = await GetReferralVerificationHospitalIdAsync(model.ReferralId, cancellationToken);
        if (!hospitalId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Your account is not linked to a referral facility.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCodeVerificationModelAsync(model, cancellationToken);
            return View(model);
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        string? userId = currentUser?.Id;
        string? userName = currentUser?.FullName
            ?? currentUser?.Email
            ?? User.Identity?.Name;

        ReferralCodeVerificationResult result = await _referralService.VerifyReferralCodeAsync(
            model,
            hospitalId!.Value,
            userId,
            userName,
            cancellationToken);

        if (!result.Succeeded || !result.ReferralId.HasValue)
        {
            ModelState.AddModelError(nameof(model.Code), result.Message);
            await PopulateCodeVerificationModelAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.ReferralId.Value });
    }

    [HttpGet("Encounter/{id:guid}")]
    public async Task<IActionResult> Encounter(Guid id, CancellationToken cancellationToken = default)
    {
        Referral? referral = await GetReferralWithDetailsAsync(id, cancellationToken);
        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        if (referral.Status != ReferralStatus.Received)
        {
            TempData["Error"] = "Receive the verified referral before recording the referral encounter.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ReferralHospitalEncounterViewModel model = BuildEncounterViewModel(referral);
        await PopulateEncounterCatalogAsync(model, referral, cancellationToken);
        PopulateEncounterLists(model);
        return View(model);
    }

    [HttpPost("Encounter/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encounter(
        Guid id,
        ReferralHospitalEncounterViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (id != model.ReferralId)
        {
            return BadRequest();
        }

        Referral? referral = await GetReferralWithDetailsAsync(id, cancellationToken);
        if (referral == null)
        {
            return NotFound();
        }

        if (!await CanAccessReferralAsync(referral, cancellationToken))
        {
            return Forbid();
        }

        if (referral.Status != ReferralStatus.Received)
        {
            TempData["Error"] = "This referral must be received before an encounter can be submitted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateEncounterCatalogAsync(model, referral, cancellationToken);
        NormalizePostedModel(model, referral);
        ApplyClaimSupportCatalog(model);
        ValidateEncounterInput(model);
        ValidateClaimSupportingDocuments(model);

        Enrollee? enrollee = await _context.Enrollees
            .Include(x => x.Hmo)
            .FirstOrDefaultAsync(x => x.EnrollmentNumber == referral.EnrolleeNumber, cancellationToken);

        if (enrollee == null)
        {
            ModelState.AddModelError(string.Empty, "The referred enrollee could not be found in the enrollee register.");
        }
        else if (enrollee.HmoId == null)
        {
            ModelState.AddModelError(string.Empty, "The referred enrollee is not linked to an HMO, so a claim cannot be submitted.");
        }

        Hmo? hmo = null;
        if (enrollee?.HmoId != null)
        {
            hmo = enrollee.Hmo ?? await _context.Hmos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == enrollee.HmoId.Value, cancellationToken);

            if (hmo == null)
            {
                ModelState.AddModelError(string.Empty, "The referred enrollee's HMO record could not be found.");
            }
        }

        if (!ModelState.IsValid)
        {
            PopulateEncounterLists(model);
            return View(model);
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        string actorName = currentUser?.FullName
            ?? currentUser?.Email
            ?? User.Identity?.Name
            ?? "ReferralPro";

        List<string> savedClaimSupportFiles = new();

        await using var transaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            Provider provider = await GetOrCreateReferralProviderAsync(
                referral.ReferredHospital!,
                hmo!.Id,
                cancellationToken);

            int lastEncounterId = await _context.Encounters
                .OrderByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? 0;

            Encounter encounter = new()
            {
                EnrolleeId = enrollee!.Id,
                ProviderId = provider.Id,
                VisitDate = TrimToSecond(model.VisitDate),
                ChiefComplaint = model.ChiefComplaint.Trim(),
                Diagnosis = model.Diagnosis.Trim(),
                TreatmentGiven = model.TreatmentGiven.Trim(),
                ConsultationFee = model.SurgeryFee,
                LabFee = model.LabFee,
                DrugFee = model.DrugFee,
                Status = "Claimed",
                VisitType = model.VisitType.Trim(),
                ServiceSetting = model.ServiceSetting.Trim(),
                Notes = BuildEncounterNotes(model, referral),
                AttendedBy = actorName,
                SeenBy = actorName,
                Rank = "ReferralPro",
                EncounterNumber = $"REF-ECN-{DateTime.Now:yyyy}-{(lastEncounterId + 1):D6}",
                IsBilled = false,
                FeesWaived = model.FeesWaived
            };

            foreach (string service in model.SelectedServices)
            {
                encounter.Services.Add(new EncounterService
                {
                    ServiceSetting = encounter.ServiceSetting,
                    ServiceName = service
                });
            }

            _context.Encounters.Add(encounter);
            await _context.SaveChangesAsync(cancellationToken);

            AppClaim claim = new()
            {
                ClaimNumber = "RCLM-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                EnrolleeId = enrollee.Id,
                ProviderId = provider.Id,
                HmoId = hmo.Id,
                Amount = encounter.TotalAmount,
                Diagnosis = encounter.Diagnosis ?? "Referral encounter",
                Treatment = BuildClaimTreatmentSummary(model, encounter.TreatmentGiven),
                DateSubmitted = DateTime.Now,
                Status = "Submitted",
                SubmittedBy = actorName
            };

            _context.Claims.Add(claim);
            await _context.SaveChangesAsync(cancellationToken);

            List<ClaimSupportingDocument> supportingDocuments = await SaveClaimSupportingDocumentsAsync(
                model,
                claim,
                currentUser,
                actorName,
                savedClaimSupportFiles,
                cancellationToken);

            _context.ClaimSupportingDocuments.AddRange(supportingDocuments);
            await _context.SaveChangesAsync(cancellationToken);

            encounter.ClaimId = claim.Id;
            referral.Status = ReferralStatus.Closed;
            referral.EncounterReference = encounter.EncounterNumber;
            referral.ReferralVerificationCodeExpiresAt = DateTime.UtcNow;

            AddReferralAuditLog(
                referral.Id,
                ReferralAuditAction.EncounterSubmitted,
                currentUser,
                $"Referral encounter {encounter.EncounterNumber} submitted by referred facility.");
            AddReferralAuditLog(
                referral.Id,
                ReferralAuditAction.ClaimSubmitted,
                currentUser,
                $"Claim {claim.ClaimNumber} submitted to {hmo.Name}.");
            AddReferralAuditLog(
                referral.Id,
                ReferralAuditAction.Closed,
                currentUser,
                "Referral closed after encounter and claim submission. Referral verification code expired.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _notificationService.NotifyClaimSubmittedAsync(
                claim.Id,
                referral.Id,
                cancellationToken);

            TempData["Success"] = $"Referral encounter {encounter.EncounterNumber} saved and claim {claim.ClaimNumber} submitted to {hmo.Name}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception) when (
            exception is DbUpdateException ||
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DeleteSavedClaimSupportFiles(savedClaimSupportFiles);
            ModelState.AddModelError(string.Empty, "The referral encounter, claim, and supporting documents could not be saved. Please review the form and try again.");
        }

        PopulateEncounterLists(model);
        return View(model);
    }

    private IQueryable<Referral> BuildReferralProQuery(ReferredHospital? currentHospital)
    {
        ReferralStatus[] visibleStatuses =
        {
            ReferralStatus.Verified,
            ReferralStatus.Audited,
            ReferralStatus.Received,
            ReferralStatus.Closed
        };

        IQueryable<Referral> query = _context.Referrals
            .AsNoTracking()
            .Include(x => x.ReferredHospital)
            .Where(x => !x.IsDeleted && visibleStatuses.Contains(x.Status));

        if (!User.IsInRole("CTSHIPAdmin") && currentHospital != null)
        {
            query = query.Where(x => x.ReferredHospitalId == currentHospital.Id);
        }

        return query;
    }

    private async Task<ReferralProviderClaimMetrics> GetReferralClaimMetricsAsync(
        ReferredHospital? currentHospital,
        CancellationToken cancellationToken)
    {
        IQueryable<AppClaim> query = _context.Claims.AsNoTracking();

        if (currentHospital == null)
        {
            query = query.Where(x => x.ClaimNumber.StartsWith("RCLM-"));
        }
        else
        {
            List<int> providerIds = await GetReferralProviderIdsAsync(currentHospital, cancellationToken);
            if (providerIds.Count == 0)
            {
                return new ReferralProviderClaimMetrics();
            }

            query = query.Where(x => providerIds.Contains(x.ProviderId));
        }

        string[] pendingStatuses = { "Submitted", "ReApproved", "Under Review" };
        string[] approvedStatuses = { "Approved", "Review Approved" };

        return new ReferralProviderClaimMetrics
        {
            TotalClaims = await query.CountAsync(cancellationToken),
            PendingClaims = await query.CountAsync(x => pendingStatuses.Contains(x.Status), cancellationToken),
            ApprovedClaims = await query.CountAsync(x => approvedStatuses.Contains(x.Status), cancellationToken),
            PaidClaims = await query.CountAsync(x => x.Status == "Paid", cancellationToken),
            RejectedClaims = await query.CountAsync(x => x.Status == "Rejected", cancellationToken),
            TotalClaimValue = await query.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m,
            PaidClaimValue = await query
                .Where(x => x.Status == "Paid")
                .SumAsync(x => (decimal?)x.Amount, cancellationToken)
                ?? 0m
        };
    }

    private async Task<ReferralProviderComplaintMetrics> GetReferralComplaintMetricsAsync(
        ReferredHospital? currentHospital,
        CancellationToken cancellationToken)
    {
        IQueryable<Complaint> query = _context.Complaints.AsNoTracking();

        if (currentHospital == null)
        {
            query = query.Where(x => x.Provider != null && x.Provider.Level == "Referral Hospital");
        }
        else
        {
            List<int> providerIds = await GetReferralProviderIdsAsync(currentHospital, cancellationToken);
            if (providerIds.Count == 0)
            {
                return new ReferralProviderComplaintMetrics();
            }

            query = query.Where(x => x.ProviderId.HasValue && providerIds.Contains(x.ProviderId.Value));
        }

        ComplaintStatus[] openStatuses = { ComplaintStatus.Open, ComplaintStatus.InProgress };
        ComplaintStatus[] resolvedStatuses = { ComplaintStatus.Resolved, ComplaintStatus.Closed };

        return new ReferralProviderComplaintMetrics
        {
            TotalComplaints = await query.CountAsync(cancellationToken),
            OpenComplaints = await query.CountAsync(x => openStatuses.Contains(x.Status), cancellationToken),
            EscalatedComplaints = await query.CountAsync(x => x.Status == ComplaintStatus.Escalated, cancellationToken),
            ResolvedComplaints = await query.CountAsync(x => resolvedStatuses.Contains(x.Status), cancellationToken)
        };
    }

    private async Task<List<int>> GetReferralProviderIdsAsync(
        ReferredHospital currentHospital,
        CancellationToken cancellationToken)
    {
        string hospitalName = currentHospital.Name;
        string? hospitalEmail = currentHospital.Email;
        bool hasHospitalEmail = !string.IsNullOrWhiteSpace(hospitalEmail);

        List<int> providerIds = await _context.Providers
            .AsNoTracking()
            .Where(provider =>
                provider.Name == hospitalName ||
                (hasHospitalEmail && provider.Email == hospitalEmail))
            .Select(provider => provider.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId.HasValue == true && !providerIds.Contains(currentUser.ProviderId.Value))
        {
            providerIds.Add(currentUser.ProviderId.Value);
        }

        return providerIds;
    }

    private async Task<Referral?> GetReferralWithDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Referrals
            .Include(x => x.ReferredHospital)
            .Include(x => x.AuditLogs)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    private async Task<bool> CanAccessReferralAsync(Referral referral, CancellationToken cancellationToken)
    {
        if (User.IsInRole("CTSHIPAdmin"))
        {
            return true;
        }

        ReferredHospital? currentHospital = await GetCurrentReferralHospitalAsync(cancellationToken);
        return currentHospital != null && referral.ReferredHospitalId == currentHospital.Id;
    }

    private async Task<ReferredHospital?> GetCurrentReferralHospitalAsync(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("ReferralPro") || User.IsInRole("CTSHIPAdmin"))
        {
            return null;
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId.HasValue == true)
        {
            Provider? linkedProvider = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentUser.ProviderId.Value, cancellationToken);

            if (linkedProvider != null)
            {
                ReferredHospital? linkedHospital = await _context.ReferralHospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        (x.Name == linkedProvider.Name ||
                         (!string.IsNullOrWhiteSpace(x.Email) && x.Email == linkedProvider.Email)),
                        cancellationToken);

                if (linkedHospital != null)
                {
                    return linkedHospital;
                }
            }
        }

        string? email = currentUser?.Email ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string normalizedEmail = email.Trim().ToUpperInvariant();
        return await _context.ReferralHospitals
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.IsActive &&
                x.Email != null &&
                x.Email.ToUpper() == normalizedEmail,
                cancellationToken);
    }

    private static ReferralDetailsViewModel MapReferralDetails(Referral referral)
    {
        return new ReferralDetailsViewModel
        {
            Id = referral.Id,
            EncounterId = referral.EncounterId,
            EncounterReference = referral.EncounterReference,
            EnrolleeNumber = referral.EnrolleeNumber,
            EnrolleeFullName = referral.EnrolleeFullName,
            HmoCode = referral.HmoCode,
            HmoName = referral.HmoName,
            FromProviderId = referral.FromProviderId,
            FromProviderName = referral.FromProviderName,
            ReferredHospitalName = referral.ReferredHospital?.Name ?? string.Empty,
            ReferredHospitalAddress = referral.ReferredHospital?.Address,
            Diagnosis = referral.Diagnosis,
            ReasonForReferral = referral.ReasonForReferral,
            ClinicalSummary = referral.ClinicalSummary,
            TreatmentGiven = referral.TreatmentGiven,
            InvestigationSummary = referral.InvestigationSummary,
            Priority = referral.Priority,
            Status = referral.Status,
            CreatedByName = referral.CreatedByName,
            CreatedAt = referral.CreatedAt,
            SubmittedToHmoAt = referral.SubmittedToHmoAt,
            VerifiedByName = referral.VerifiedByName,
            VerifiedAt = referral.VerifiedAt,
            HmoVerificationNote = referral.HmoVerificationNote,
            ReferralVerificationCode = referral.ReferralVerificationCode,
            ReferralVerificationCodeIssuedAt = referral.ReferralVerificationCodeIssuedAt,
            ReferralVerificationCodeExpiresAt = referral.ReferralVerificationCodeExpiresAt,
            ReferralVerificationCodeIssuedByName = referral.ReferralVerificationCodeIssuedByName,
            ReferralVerificationCodeVerifiedAt = referral.ReferralVerificationCodeVerifiedAt,
            ReferralVerificationCodeVerifiedByName = referral.ReferralVerificationCodeVerifiedByName,
            AuditedByName = referral.AuditedByName,
            AuditedAt = referral.AuditedAt,
            AuditNote = referral.AuditNote,
            AuditLogs = referral.AuditLogs
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ReferralAuditLogViewModel
                {
                    Action = x.Action,
                    PerformedByName = x.PerformedByName,
                    Note = x.Note,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };
    }

    private static ReferralHospitalEncounterViewModel BuildEncounterViewModel(Referral referral)
    {
        ReferralHospitalEncounterViewModel model = new()
        {
            ReferralId = referral.Id,
            EnrolleeNumber = referral.EnrolleeNumber,
            EnrolleeFullName = referral.EnrolleeFullName,
            FromProviderName = referral.FromProviderName,
            ReferredHospitalName = referral.ReferredHospital?.Name ?? string.Empty,
            HmoName = referral.HmoName,
            CatalogState = referral.ReferredHospital?.State ?? string.Empty,
            DiagnosisFromReferral = referral.Diagnosis,
            ReasonForReferral = referral.ReasonForReferral,
            VisitDate = TrimToSecond(DateTime.Now),
            VisitType = "Referral",
            ServiceSetting = EncounterServiceCatalog.Outpatient,
            ChiefComplaint = referral.ReasonForReferral,
            Diagnosis = referral.Diagnosis,
            TreatmentGiven = referral.TreatmentGiven ?? string.Empty,
            FeesWaived = true,
            Notes = referral.ClinicalSummary
        };

        model.SelectedServices.Add("Management of common infectious diseases");
        return model;
    }

    private async Task PopulateEncounterCatalogAsync(
        ReferralHospitalEncounterViewModel model,
        Referral referral,
        CancellationToken cancellationToken)
    {
        string? catalogState = NormalizeReferralCatalogState(referral.ReferredHospital?.State ?? model.CatalogState);
        model.CatalogState = catalogState ?? referral.ReferredHospital?.State ?? model.CatalogState ?? string.Empty;
        model.PrescriptionCatalog = new List<ReferralEncounterClaimCatalogItem>();
        model.LaboratoryCatalog = new List<ReferralEncounterClaimCatalogItem>();
        model.SurgeryCatalog = new List<ReferralEncounterClaimCatalogItem>();

        if (catalogState == null)
        {
            return;
        }

        List<ReferralPriceCatalogItem> catalogItems = await _context.ReferralPriceCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.State == catalogState)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);

        model.PrescriptionCatalog = BuildCatalogItems(
            catalogItems,
            ReferralEncounterClaimCatalog.PrescriptionService);
        model.LaboratoryCatalog = BuildCatalogItems(
            catalogItems,
            ReferralEncounterClaimCatalog.LaboratoryService);
        model.SurgeryCatalog = BuildCatalogItems(
            catalogItems,
            ReferralEncounterClaimCatalog.SurgeryService);
    }

    private static List<ReferralEncounterClaimCatalogItem> BuildCatalogItems(
        IEnumerable<ReferralPriceCatalogItem> catalogItems,
        string category)
    {
        return catalogItems
            .Where(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(item => new ReferralEncounterClaimCatalogItem(item.Title, item.Price))
            .ToList();
    }

    private static string? NormalizeReferralCatalogState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return NorthEastLocationData.States.FirstOrDefault(candidate =>
            string.Equals(candidate, state.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void PopulateEncounterLists(ReferralHospitalEncounterViewModel model)
    {
        model.ServiceSettings = new List<SelectListItem>
        {
            new(EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Outpatient)
            {
                Selected = string.Equals(model.ServiceSetting, EncounterServiceCatalog.Outpatient, StringComparison.OrdinalIgnoreCase)
            },
            new(EncounterServiceCatalog.Inpatient, EncounterServiceCatalog.Inpatient)
            {
                Selected = string.Equals(model.ServiceSetting, EncounterServiceCatalog.Inpatient, StringComparison.OrdinalIgnoreCase)
            }
        };

        model.VisitTypes = new List<SelectListItem>
        {
            new("Referral", "Referral")
            {
                Selected = string.Equals(model.VisitType, "Referral", StringComparison.OrdinalIgnoreCase)
            },
            new("Emergency", "Emergency")
            {
                Selected = string.Equals(model.VisitType, "Emergency", StringComparison.OrdinalIgnoreCase)
            },
            new("Follow-up", "Follow-up")
            {
                Selected = string.Equals(model.VisitType, "Follow-up", StringComparison.OrdinalIgnoreCase)
            }
        };
    }

    private static void NormalizePostedModel(ReferralHospitalEncounterViewModel model, Referral referral)
    {
        model.EnrolleeNumber = referral.EnrolleeNumber;
        model.EnrolleeFullName = referral.EnrolleeFullName;
        model.FromProviderName = referral.FromProviderName;
        model.ReferredHospitalName = referral.ReferredHospital?.Name ?? string.Empty;
        model.HmoName = referral.HmoName;
        model.DiagnosisFromReferral = referral.Diagnosis;
        model.ReasonForReferral = referral.ReasonForReferral;

        model.VisitType = string.IsNullOrWhiteSpace(model.VisitType)
            ? "Referral"
            : model.VisitType.Trim();
        model.ServiceSetting = string.IsNullOrWhiteSpace(model.ServiceSetting)
            ? EncounterServiceCatalog.Outpatient
            : model.ServiceSetting.Trim();
        model.ChiefComplaint = model.ChiefComplaint?.Trim() ?? string.Empty;
        model.Diagnosis = model.Diagnosis?.Trim() ?? string.Empty;
        model.TreatmentGiven = model.TreatmentGiven?.Trim() ?? string.Empty;
        model.SelectedServices = model.SelectedServices
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        model.SelectedPrescriptions = ReferralEncounterClaimCatalog.NormalizeSelection(
            model.SelectedPrescriptions,
            model.PrescriptionCatalog);
        model.SelectedLaboratoryTests = ReferralEncounterClaimCatalog.NormalizeSelection(
            model.SelectedLaboratoryTests,
            model.LaboratoryCatalog);
        model.SelectedSurgeries = ReferralEncounterClaimCatalog.NormalizeSelection(
            model.SelectedSurgeries,
            model.SurgeryCatalog);
    }

    private static void ApplyClaimSupportCatalog(ReferralHospitalEncounterViewModel model)
    {
        model.ConsultationFee = 0m;
        model.DrugFee = ReferralEncounterClaimCatalog.SumSelected(
            model.SelectedPrescriptions,
            model.PrescriptionCatalog);
        model.LabFee = ReferralEncounterClaimCatalog.SumSelected(
            model.SelectedLaboratoryTests,
            model.LaboratoryCatalog);
        model.SurgeryFee = ReferralEncounterClaimCatalog.SumSelected(
            model.SelectedSurgeries,
            model.SurgeryCatalog);
        model.FeesWaived = model.TotalAmount <= 0m;
    }

    private void ValidateEncounterInput(ReferralHospitalEncounterViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ChiefComplaint))
        {
            ModelState.AddModelError(nameof(model.ChiefComplaint), "Complaint is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Diagnosis))
        {
            ModelState.AddModelError(nameof(model.Diagnosis), "Diagnosis is required.");
        }

        if (string.IsNullOrWhiteSpace(model.TreatmentGiven))
        {
            ModelState.AddModelError(nameof(model.TreatmentGiven), "Treatment given is required.");
        }

        if (model.VisitDate > DateTime.Now)
        {
            ModelState.AddModelError(nameof(model.VisitDate), "Encounter date cannot be in the future.");
        }

        if (model.SelectedServices.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedServices), "Select at least one service delivered.");
        }
        else if (model.SelectedServices.Any(x =>
            !EncounterServiceCatalog.IsValid(model.ServiceSetting, x)
            && !ReferralEncounterClaimCatalog.IsClaimSupportService(x)))
        {
            ModelState.AddModelError(nameof(model.SelectedServices), "One or more services do not match the selected service setting.");
        }

        if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.PrescriptionService, StringComparer.OrdinalIgnoreCase)
            && model.PrescriptionCatalog.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedPrescriptions),
                $"No active prescription price catalog is configured for {model.CatalogState}.");
        }
        else if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.PrescriptionService, StringComparer.OrdinalIgnoreCase)
            && model.SelectedPrescriptions.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedPrescriptions), "Select at least one prescription item.");
        }

        if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.LaboratoryService, StringComparer.OrdinalIgnoreCase)
            && model.LaboratoryCatalog.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedLaboratoryTests),
                $"No active laboratory price catalog is configured for {model.CatalogState}.");
        }
        else if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.LaboratoryService, StringComparer.OrdinalIgnoreCase)
            && model.SelectedLaboratoryTests.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedLaboratoryTests), "Select at least one laboratory test.");
        }

        if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.SurgeryService, StringComparer.OrdinalIgnoreCase)
            && model.SurgeryCatalog.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedSurgeries),
                $"No active surgery price catalog is configured for {model.CatalogState}.");
        }
        else if (model.SelectedServices.Contains(ReferralEncounterClaimCatalog.SurgeryService, StringComparer.OrdinalIgnoreCase)
            && model.SelectedSurgeries.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedSurgeries), "Select at least one surgery.");
        }

        if (model.ConsultationFee < 0 || model.LabFee < 0 || model.DrugFee < 0 || model.SurgeryFee < 0)
        {
            ModelState.AddModelError(string.Empty, "Fees cannot be negative.");
        }

        if (model.SelectedServices.Count > 0 && model.TotalAmount <= 0 && !model.FeesWaived)
        {
            ModelState.AddModelError(string.Empty, "Enter at least one fee amount or tick Fees Waived.");
        }

        if (model.TotalAmount > 0 && model.FeesWaived)
        {
            ModelState.AddModelError(nameof(model.FeesWaived), "Fees Waived can only be used when all fees are zero.");
        }
    }

    private void ValidateClaimSupportingDocuments(ReferralHospitalEncounterViewModel model)
    {
        if (model.FindingsEvidenceFile == null || model.FindingsEvidenceFile.Length == 0)
        {
            ModelState.AddModelError(
                nameof(model.FindingsEvidenceFile),
                "Upload evidence of findings before submitting the claim to the HMO.");
        }
        else
        {
            ValidateClaimSupportFile(model.FindingsEvidenceFile, nameof(model.FindingsEvidenceFile));
        }

        model.SupportingDocumentFiles = (model.SupportingDocumentFiles ?? new List<IFormFile>())
            .Where(file => file is { Length: > 0 })
            .ToList();

        if (model.SupportingDocumentFiles.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SupportingDocumentFiles),
                "Upload at least one supporting claim document before submitting the claim to the HMO.");
        }

        if (model.SupportingDocumentFiles.Count > MaxSupportingDocumentCount)
        {
            ModelState.AddModelError(
                nameof(model.SupportingDocumentFiles),
                $"Upload {MaxSupportingDocumentCount} or fewer supporting documents.");
        }

        foreach (IFormFile file in model.SupportingDocumentFiles)
        {
            ValidateClaimSupportFile(file, nameof(model.SupportingDocumentFiles));
        }
    }

    private void ValidateClaimSupportFile(IFormFile file, string modelStateKey)
    {
        string extension = Path.GetExtension(file.FileName);
        if (!AllowedClaimSupportFileExtensions.Contains(extension))
        {
            ModelState.AddModelError(
                modelStateKey,
                $"{file.FileName} must be a PDF, JPG, PNG, DOC, or DOCX file.");
        }

        if (file.Length > MaxClaimSupportFileBytes)
        {
            ModelState.AddModelError(
                modelStateKey,
                $"{file.FileName} must be 10MB or smaller.");
        }
    }

    private async Task<List<ClaimSupportingDocument>> SaveClaimSupportingDocumentsAsync(
        ReferralHospitalEncounterViewModel model,
        AppClaim claim,
        ApplicationUser? currentUser,
        string actorName,
        List<string> savedPhysicalPaths,
        CancellationToken cancellationToken)
    {
        string webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _environment.WebRootPath;
        string claimNumber = SanitizePathSegment(claim.ClaimNumber);
        string uploadFolder = Path.Combine(webRootPath, "uploads", "claim-support", claimNumber);

        Directory.CreateDirectory(uploadFolder);

        List<ClaimSupportingDocument> documents = new();

        if (model.FindingsEvidenceFile != null)
        {
            documents.Add(await SaveClaimSupportFileAsync(
                model.FindingsEvidenceFile,
                claim.Id,
                claimNumber,
                "Evidence of Finding",
                uploadFolder,
                currentUser,
                actorName,
                savedPhysicalPaths,
                cancellationToken));
        }

        foreach (IFormFile file in model.SupportingDocumentFiles)
        {
            documents.Add(await SaveClaimSupportFileAsync(
                file,
                claim.Id,
                claimNumber,
                "Supporting Document",
                uploadFolder,
                currentUser,
                actorName,
                savedPhysicalPaths,
                cancellationToken));
        }

        return documents;
    }

    private static async Task<ClaimSupportingDocument> SaveClaimSupportFileAsync(
        IFormFile file,
        int claimId,
        string claimNumber,
        string documentType,
        string uploadFolder,
        ApplicationUser? currentUser,
        string actorName,
        List<string> savedPhysicalPaths,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string storedFileName = $"{documentType.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}{extension}";
        string physicalPath = Path.Combine(uploadFolder, storedFileName);

        await using (FileStream stream = new(physicalPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        savedPhysicalPaths.Add(physicalPath);

        return new ClaimSupportingDocument
        {
            ClaimId = claimId,
            DocumentType = documentType,
            OriginalFileName = TrimForStorage(Path.GetFileName(file.FileName), 255),
            StoredFileName = storedFileName,
            FilePath = $"/uploads/claim-support/{claimNumber}/{storedFileName}",
            ContentType = TrimForStorage(file.ContentType, 100),
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = currentUser?.Id,
            UploadedByName = actorName
        };
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value
            .Where(character => !invalidChars.Contains(character))
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }

    private static string TrimForStorage(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private static void DeleteSavedClaimSupportFiles(IEnumerable<string> physicalPaths)
    {
        foreach (string physicalPath in physicalPaths)
        {
            try
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch
            {
                // Best effort cleanup after a failed claim transaction.
            }
        }
    }

    private async Task<Provider> GetOrCreateReferralProviderAsync(
        ReferredHospital hospital,
        int hmoId,
        CancellationToken cancellationToken)
    {
        string providerCode = BuildReferralProviderCode(hospital.Id, hmoId);
        Provider? provider = await _context.Providers.FirstOrDefaultAsync(x =>
            x.HmoId == hmoId &&
            (x.Code == providerCode ||
             x.Name == hospital.Name ||
             (!string.IsNullOrWhiteSpace(hospital.Email) && x.Email == hospital.Email)),
            cancellationToken);

        if (provider != null)
        {
            if (!provider.IsActive)
            {
                provider.IsActive = true;
            }

            provider.Code = string.IsNullOrWhiteSpace(provider.Code) ? providerCode : provider.Code;
            provider.Level = string.IsNullOrWhiteSpace(provider.Level) ? "Referral Hospital" : provider.Level;
            provider.State = string.IsNullOrWhiteSpace(provider.State) ? hospital.State ?? "N/A" : provider.State;
            provider.LGA = string.IsNullOrWhiteSpace(provider.LGA) ? hospital.Lga ?? "N/A" : provider.LGA;
            provider.Location = string.IsNullOrWhiteSpace(provider.Location) ? hospital.Address : provider.Location;
            provider.Phone = string.IsNullOrWhiteSpace(provider.Phone) ? hospital.PhoneNumber ?? "N/A" : provider.Phone;
            provider.Email = string.IsNullOrWhiteSpace(provider.Email) ? hospital.Email ?? string.Empty : provider.Email;
            await _context.SaveChangesAsync(cancellationToken);
            return provider;
        }

        provider = new Provider
        {
            Name = hospital.Name,
            Location = hospital.Address,
            IsActive = true,
            PatientRatio = 0,
            State = string.IsNullOrWhiteSpace(hospital.State) ? "N/A" : hospital.State,
            LGA = string.IsNullOrWhiteSpace(hospital.Lga) ? "N/A" : hospital.Lga,
            Phone = string.IsNullOrWhiteSpace(hospital.PhoneNumber) ? "N/A" : hospital.PhoneNumber,
            Email = hospital.Email ?? string.Empty,
            Code = providerCode,
            Level = "Referral Hospital",
            DateRegistered = DateTime.Now,
            HmoId = hmoId
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        return provider;
    }

    private static string BuildReferralProviderCode(Guid hospitalId, int hmoId)
    {
        return $"REF-{hmoId}-{hospitalId:N}"[..18].ToUpperInvariant();
    }

    private static DateTime TrimToSecond(DateTime value)
    {
        return new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Kind);
    }

    private static string BuildEncounterNotes(ReferralHospitalEncounterViewModel model, Referral referral)
    {
        string notes = string.IsNullOrWhiteSpace(model.Notes) ? string.Empty : model.Notes.Trim();
        string prefix = $"Referral ID: {referral.Id}; From Provider: {referral.FromProviderName};";
        string claimSupport = BuildClaimSupportSummary(model);
        return string.Join(
            Environment.NewLine,
            new[] { prefix, claimSupport, notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string BuildClaimTreatmentSummary(ReferralHospitalEncounterViewModel model, string? treatmentGiven)
    {
        string treatment = string.IsNullOrWhiteSpace(treatmentGiven)
            ? "Referral care"
            : treatmentGiven.Trim();
        string claimSupport = BuildClaimSupportSummary(model);
        return string.IsNullOrWhiteSpace(claimSupport)
            ? treatment
            : treatment + Environment.NewLine + claimSupport;
    }

    private static string BuildClaimSupportSummary(ReferralHospitalEncounterViewModel model)
    {
        List<string> lines = new()
        {
            ReferralEncounterClaimCatalog.DescribeSelected(
                "Prescription",
                model.SelectedPrescriptions,
                model.PrescriptionCatalog),
            ReferralEncounterClaimCatalog.DescribeSelected(
                "Laboratory",
                model.SelectedLaboratoryTests,
                model.LaboratoryCatalog),
            ReferralEncounterClaimCatalog.DescribeSelected(
                "Surgery",
                model.SelectedSurgeries,
                model.SurgeryCatalog)
        };

        return string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private void AddReferralAuditLog(
        Guid referralId,
        ReferralAuditAction action,
        ApplicationUser? currentUser,
        string note)
    {
        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referralId,
            Action = action,
            PerformedByUserId = currentUser?.Id,
            PerformedByName = currentUser?.FullName
                ?? currentUser?.Email
                ?? User.Identity?.Name,
            Note = note
        });
    }

    private static bool RequiresReferralCodeVerification(Referral referral)
    {
        return (referral.Status == ReferralStatus.Verified || referral.Status == ReferralStatus.Audited)
            && !referral.ReferralVerificationCodeVerifiedAt.HasValue;
    }

    private async Task<bool> PopulateCodeVerificationModelAsync(
        ReferralCodeVerificationViewModel model,
        CancellationToken cancellationToken)
    {
        ReferredHospital? currentHospital = await GetCurrentReferralHospitalAsync(cancellationToken);
        if (currentHospital != null)
        {
            model.ReferredHospitalName = currentHospital.Name;
        }

        if (model.ReferralId.HasValue)
        {
            Referral? referral = await _context.Referrals
                .AsNoTracking()
                .Include(x => x.ReferredHospital)
                .FirstOrDefaultAsync(x => x.Id == model.ReferralId.Value && !x.IsDeleted, cancellationToken);

            if (referral != null)
            {
                model.ReferredHospitalName = referral.ReferredHospital?.Name ?? model.ReferredHospitalName;
                model.EnrolleeNumberHint = referral.EnrolleeNumber;
            }
        }

        return User.IsInRole("CTSHIPAdmin") || currentHospital != null;
    }

    private async Task<Guid?> GetReferralVerificationHospitalIdAsync(
        Guid? referralId,
        CancellationToken cancellationToken)
    {
        ReferredHospital? currentHospital = await GetCurrentReferralHospitalAsync(cancellationToken);
        if (currentHospital != null)
        {
            return currentHospital.Id;
        }

        if (!User.IsInRole("CTSHIPAdmin") || !referralId.HasValue)
        {
            return null;
        }

        return await _context.Referrals
            .AsNoTracking()
            .Where(x => x.Id == referralId.Value && !x.IsDeleted)
            .Select(x => (Guid?)x.ReferredHospitalId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void SetReferralCounts(int pendingCount, int receivedCount, int completedCount)
    {
        ViewBag.PendingCount = pendingCount;
        ViewBag.ReceivedCount = receivedCount;
        ViewBag.CompletedCount = completedCount;
    }
}
