using System.Data;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AppClaim = CTSHIPDashboard.Models.Claim;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "ReferralPro,CTSHIPAdmin")]
[Route("ReferralPro/Referrals")]
public class ReferralProController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<AnalyticsHub> _hubContext;

    public ReferralProController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IHubContext<AnalyticsHub> hubContext)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
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
                AuditedAt = x.AuditedAt
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

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        referral.Status = ReferralStatus.Received;
        AddReferralAuditLog(
            referral.Id,
            ReferralAuditAction.Received,
            currentUser,
            "Referral received by referred facility.");

        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Referral received. You can now record the referral encounter.";
        return RedirectToAction(nameof(Details), new { id });
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

        NormalizePostedModel(model, referral);
        ValidateEncounterInput(model);

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
                ConsultationFee = model.ConsultationFee,
                LabFee = model.LabFee,
                DrugFee = model.DrugFee,
                WalletSource = EncounterWalletSource.ProviderWallet,
                Status = "Claimed",
                Temperature = model.Temperature,
                BloodPressure = model.BloodPressure.Trim(),
                VisitType = model.VisitType.Trim(),
                ServiceSetting = model.ServiceSetting.Trim(),
                PulseRate = model.PulseRate,
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
                Treatment = encounter.TreatmentGiven ?? "Referral care",
                DateSubmitted = DateTime.Now,
                Status = "Submitted",
                SubmittedBy = actorName
            };

            _context.Claims.Add(claim);
            await _context.SaveChangesAsync(cancellationToken);

            encounter.ClaimId = claim.Id;
            referral.Status = ReferralStatus.Closed;
            referral.EncounterReference = encounter.EncounterNumber;

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
                "Referral closed after encounter and claim submission.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _hubContext.Clients.All.SendAsync("ClaimSubmitted", new
            {
                claim.Id,
                claim.ClaimNumber,
                EnrolleeName = enrollee.FullName,
                HmoName = hmo.Name,
                ProviderName = provider.Name,
                Amount = claim.Amount,
                Status = claim.Status
            }, cancellationToken);

            TempData["Success"] = $"Referral encounter {encounter.EncounterNumber} saved and claim {claim.ClaimNumber} submitted to {hmo.Name}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, "The referral encounter could not be saved. Please review the form and try again.");
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
        model.BloodPressure = model.BloodPressure?.Trim() ?? string.Empty;
        model.SelectedServices = model.SelectedServices
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        if (model.Temperature < 35m || model.Temperature > 42m)
        {
            ModelState.AddModelError(nameof(model.Temperature), "Temperature must be between 35 and 42 C.");
        }

        if (model.PulseRate < 40 || model.PulseRate > 180)
        {
            ModelState.AddModelError(nameof(model.PulseRate), "Pulse rate must be between 40 and 180 bpm.");
        }

        if (!IsValidBloodPressure(model.BloodPressure))
        {
            ModelState.AddModelError(nameof(model.BloodPressure), "Blood pressure must be in systolic/diastolic format, for example 120/80.");
        }

        if (model.SelectedServices.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedServices), "Select at least one service delivered.");
        }
        else if (model.SelectedServices.Any(x => !EncounterServiceCatalog.IsValid(model.ServiceSetting, x)))
        {
            ModelState.AddModelError(nameof(model.SelectedServices), "One or more services do not match the selected service setting.");
        }

        if (model.ConsultationFee < 0 || model.LabFee < 0 || model.DrugFee < 0)
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

    private static bool IsValidBloodPressure(string? bloodPressure)
    {
        if (string.IsNullOrWhiteSpace(bloodPressure))
        {
            return false;
        }

        string[] parts = bloodPressure.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int systolic) ||
            !int.TryParse(parts[1], out int diastolic))
        {
            return false;
        }

        return systolic >= 70 &&
            systolic <= 250 &&
            diastolic >= 40 &&
            diastolic <= 150 &&
            systolic > diastolic;
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
        return string.IsNullOrWhiteSpace(notes) ? prefix : $"{prefix} {notes}";
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

    private void SetReferralCounts(int pendingCount, int receivedCount, int completedCount)
    {
        ViewBag.PendingCount = pendingCount;
        ViewBag.ReceivedCount = receivedCount;
        ViewBag.CompletedCount = completedCount;
    }
}
