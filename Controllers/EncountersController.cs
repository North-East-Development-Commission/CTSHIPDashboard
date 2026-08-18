using CTSHIPDashboard.Data;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,CTSHIPAdmin")]
    public class EncountersController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CTSHIPDashboard.Services.IAuditService _auditService;
        private readonly IAppNotificationService _notificationService;

        public EncountersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CTSHIPDashboard.Services.IAuditService auditService,
            IAppNotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _notificationService = notificationService;
        }

        // INDEX â€” FULL LIST WITH SEARCH & FILTER
        public async Task<IActionResult> Index(string search = "", string status = "All", int page = 1, int pageSize = 10)
        {
            var query = _context.Encounters
                .Include(e => e.Enrollee)
                    .ThenInclude(e => e!.Hmo)
                .Include(e => e.Provider)
                .Include(e => e.Doctor)
                .Include(e => e.Claim)
                .AsQueryable();

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("Provider"))
            {
                if (!currentUser?.ProviderId.HasValue ?? true)
                {
                    return Forbid();
                }

                query = query.Where(encounter => encounter.ProviderId == currentUser!.ProviderId!.Value);
                string? providerLevel = await _context.Providers
                    .Where(provider => provider.Id == currentUser!.ProviderId!.Value)
                    .Select(provider => provider.Level)
                    .FirstOrDefaultAsync();
                ViewBag.CanUseClaims = ProviderClaimAccessHelper.CanUseClaims(providerLevel);
            }
            else
            {
                ViewBag.CanUseClaims = true;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(e =>
                    e.Enrollee!.FullName.Contains(s) ||
                    e.Enrollee!.EnrollmentNumber.Contains(s) ||
                    e.EncounterNumber.Contains(s));
            }

            if (status != "All")
                query = query.Where(e => e.PatientOutcome == status);

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
            ViewBag.PageSize = pageSize;
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
                .Include(e => e.Doctor)
                .Include(e => e.Claim)
                .Include(e => e.Services)
                .Include(e => e.Prescriptions)
                .Include(e => e.Queries)
                .Include(e => e.AuditTrails)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (encounter == null) return NotFound();
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();
            ViewBag.CanUseClaims = ProviderClaimAccessHelper.CanUseClaims(encounter.Provider);
            return View(encounter);
        }

        // CREATE GET
        public async Task<IActionResult> Create()
        {
            Encounter model = BuildDefaultEncounter();
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("Provider"))
            {
                if (!currentUser?.ProviderId.HasValue ?? true)
                {
                    TempData["Error"] = "Your account is not linked to a healthcare facility.";
                    return RedirectToAction("Index", "Home");
                }

                model.ProviderId = currentUser!.ProviderId!.Value;
            }

            EnsureEncounterDefaults(model);
            await PrepareEncounterFormAsync(model);
            return View(model);
        }

        // CREATE POST â€” CLEAN & PERFECT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Encounter encounter)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("Provider"))
            {
                if (!currentUser?.ProviderId.HasValue ?? true)
                {
                    return Forbid();
                }

                encounter.ProviderId = currentUser!.ProviderId!.Value;
                ModelState.Remove(nameof(encounter.ProviderId));
            }

            EnsureEncounterDefaults(encounter);
            ApplyClinicalSelections(encounter);
            ValidateSelectedServices(encounter);
            // If presenting complaints were selected on the form, synthesize ChiefComplaint
            if (encounter.SelectedPresentingComplaints != null && encounter.SelectedPresentingComplaints.Count > 0)
            {
                var list = encounter.SelectedPresentingComplaints
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList();
                // handle Other text
                if (!string.IsNullOrWhiteSpace(encounter.PresentingComplaintsOther)
                    && list.Exists(x => string.Equals(x, "Other", System.StringComparison.OrdinalIgnoreCase)))
                {
                    list = list.Where(x => !string.Equals(x, "Other", System.StringComparison.OrdinalIgnoreCase)).ToList();
                    list.Add($"Other: {encounter.PresentingComplaintsOther.Trim()}");
                }

                if (list.Count > 0)
                {
                    encounter.ChiefComplaint = string.Join(", ", list);
                }
            }
            ValidateEncounterInput(encounter);
            Enrollee? selectedEnrollee =
                await ValidateEnrolleeAssignmentAsync(encounter.EnrolleeId, encounter.ProviderId);
            Provider? selectedProvider = await ValidateProviderSelectionAsync(encounter.ProviderId);
            ValidateSelectedPrescriptions(encounter);
            await ValidateReferralInputAsync(encounter, selectedEnrollee);
            Doctor? attendingDoctor = await ValidateDoctorSelectionAsync(
                encounter.ProviderId,
                encounter.DoctorId,
                activeOnly: true);

            if (ModelState.IsValid)
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var lastEncounterId = await _context.Encounters
                        .OrderByDescending(e => e.Id)
                        .Select(e => (int?)e.Id)
                        .FirstOrDefaultAsync();

                    encounter.EncounterNumber =
                        $"ECN-{DateTime.Now:yyyy}-{(lastEncounterId.GetValueOrDefault() + 1):D6}";

                    encounter.AttendedBy = currentUser?.FullName
                        ?? currentUser?.Email
                        ?? "Unknown User";
                    ApplyDoctorSnapshot(encounter, attendingDoctor!);
                    encounter.VisitDate = TrimToSecond(encounter.VisitDate);
                    encounter.IsBilled = false;

                    SetEncounterServices(encounter);
                    SetEncounterPrescriptions(encounter);
                    // Map selected laboratory investigations into encounter fields and compute lab fee
                    if (encounter.LaboratoryInvestigations != null && encounter.LaboratoryInvestigations.Count > 0)
                    {
                        // Build a human-readable investigation summary and compute lab fee from selected test names
                        var selectedNames = encounter.LaboratoryInvestigations
                            .Where(x => !x.IsDeleted)
                            .Select(x => x.LaboratoryTestName.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        encounter.LabTests = selectedNames.Count == 0 ? null : string.Join(", ", selectedNames);
                        encounter.LabFee = await ResolveLaboratoryFeeAsync(encounter.ProviderId, selectedNames);
                        // If results were provided, append them to InvestigationSummary in LabTests for storage
                        var resultLines = encounter.LaboratoryInvestigations
                            .Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.Result))
                            .Select(x => $"{x.LaboratoryTestName}: {x.Result}{(string.IsNullOrWhiteSpace(x.ResultUnit) ? string.Empty : " " + x.ResultUnit)} ({x.ResultStatus})")
                            .ToList();
                        if (resultLines.Count > 0)
                        {
                            encounter.LabTests = (encounter.LabTests ?? string.Empty) + (encounter.LabTests != null && encounter.LabTests.Length > 0 ? " - " : string.Empty) + string.Join("; ", resultLines);
                        }
                    }
                    else
                    {
                        encounter.SelectedLaboratoryTests ??= new List<string>();
                        encounter.LabTests = encounter.SelectedLaboratoryTests.Count == 0
                            ? null
                            : string.Join(", ", encounter.SelectedLaboratoryTests.Select(s => s.Trim()));
                        encounter.LabFee = await ResolveLaboratoryFeeAsync(encounter.ProviderId, encounter.SelectedLaboratoryTests);
                    }


                    // Persist normalized presenting complaints if provided
                    if (encounter.SelectedPresentingComplaints != null && encounter.SelectedPresentingComplaints.Count > 0)
                    {
                        encounter.PresentingComplaints = encounter.SelectedPresentingComplaints
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .Select(name => new EncounterPresentingComplaint { ComplaintName = name })
                            .ToList();
                        if (!string.IsNullOrWhiteSpace(encounter.PresentingComplaintsOther)
                            && encounter.PresentingComplaints.Any(x => string.Equals(x.ComplaintName, "Other", System.StringComparison.OrdinalIgnoreCase)))
                        {
                            // replace 'Other' with 'Other: {text}'
                            var other = encounter.PresentingComplaints.FirstOrDefault(x => string.Equals(x.ComplaintName, "Other", System.StringComparison.OrdinalIgnoreCase));
                            if (other != null)
                            {
                                other.ComplaintName = $"Other: {encounter.PresentingComplaintsOther.Trim()}";
                            }
                        }
                    }

                    await ApplyPrimaryProviderCapitationSubmissionAsync(encounter, selectedEnrollee, selectedProvider, currentUser);

                    _context.Encounters.Add(encounter);
                    Guid? referralId = null;

                    if (RequiresEncounterReferral(encounter)
                        && selectedEnrollee != null
                        && selectedProvider != null)
                    {
                        Referral referral = BuildSubmittedReferralFromEncounter(
                            encounter,
                            selectedEnrollee,
                            selectedProvider,
                            currentUser);

                        _context.Referrals.Add(referral);
                        AddReferralAuditLogs(referral, currentUser);
                        referralId = referral.Id;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var actor = currentUser?.Email ?? User.Identity?.Name ?? "System";
                    await _auditService.LogAsync(
                        "Encounter.Created",
                        AuditActor.Format(currentUser, actor),
                        encounter.EncounterNumber,
                        AuditActor.Details(
                            $"Enrollee:{encounter.EnrolleeId}",
                            $"Provider:{encounter.ProviderId}",
                            "Funding: Capitation"));

                    await _notificationService.NotifyEncounterSubmittedAsync(encounter.Id, HttpContext.RequestAborted);

                    if (referralId.HasValue)
                    {
                        await _notificationService.NotifyReferralInitiatedAsync(referralId.Value);
                    }

                    TempData["Success"] = referralId.HasValue
                        ? $"Encounter {encounter.EncounterNumber} recorded and referral submitted to HMO."
                        : $"Encounter {encounter.EncounterNumber} recorded.";

                    if (User.IsInRole("Provider"))
                    {
                        return RedirectToAction("MyEncounters", "Providers");
                    }
                    if (User.IsInRole("Admin"))
                    {
                        return RedirectToAction("Index", "Enrollees");
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(
                        string.Empty,
                        "The encounter could not be completed. No funds were deducted.");
                }
            }

            await PrepareEncounterFormAsync(encounter);
            return View(encounter);
        }

        // EDIT GET â€” CLEAN
        public async Task<IActionResult> Edit(int id)
        {
            var encounter = await _context.Encounters
                .Include(x => x.Enrollee).ThenInclude(x => x!.Hmo)
                .Include(x => x.Doctor)
                .Include(x => x.Services)
                .Include(x => x.Prescriptions)
                .Include(x => x.PresentingComplaints)
                .Include(x => x.Queries)
                .Include(x => x.AuditTrails)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (encounter == null) return NotFound();
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();

            encounter.VisitDate = TrimToSecond(encounter.VisitDate);
            encounter.SelectedServices = encounter.Services.Select(x => x.ServiceName).ToList();
            encounter.SelectedPrescriptions = encounter.Prescriptions
                .Select(prescription => new EncounterPrescriptionInputViewModel
                {
                    DrugInventoryItemId = prescription.DrugInventoryItemId,
                    DrugName = prescription.DrugName,
                    Strength = prescription.Strength,
                    QuantityDispensed = prescription.QuantityDispensed,
                    StockStatus = NormalizePrescriptionStockStatus(prescription.StockStatus)
                })
                .ToList();
            await PrepareEncounterFormAsync(encounter);
            return View(encounter);
        }

        // EDIT POST â€” CLEAN & SAFE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Encounter encounter)
        {
            if (id != encounter.Id) return NotFound();

            EnsureEncounterDefaults(encounter);
            ApplyClinicalSelections(encounter);
            ValidateSelectedServices(encounter);
            ValidateEncounterInput(encounter);

            Encounter? existing = await _context.Encounters
                .Include(x => x.Enrollee)
                    .ThenInclude(x => x!.Hmo)
                .Include(x => x.Doctor)
                .Include(x => x.Services)
                .Include(x => x.Prescriptions)
                .Include(x => x.PresentingComplaints)
                .Include(x => x.Queries)
                .Include(x => x.AuditTrails)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            if (!await CanAccessProviderAsync(existing.ProviderId))
            {
                return Forbid();
            }

            encounter.ProviderId = existing.ProviderId;
            ModelState.Remove(nameof(encounter.ProviderId));
            Doctor? attendingDoctor = await ValidateDoctorSelectionAsync(
                existing.ProviderId,
                encounter.DoctorId,
                activeOnly: encounter.DoctorId != existing.DoctorId);

            if (ModelState.IsValid)
            {
                try
                {
                    existing.VisitDate = encounter.VisitDate;
                    existing.VisitDate = TrimToSecond(existing.VisitDate);
                    ApplyClinicalSelections(encounter);
                    existing.VisitType = encounter.VisitType;
                    existing.ServiceSetting = encounter.ServiceSetting;
                    existing.ReasonForEncounter = encounter.ReasonForEncounter;
                    existing.Status = encounter.Status;
                    existing.PatientOutcome = encounter.PatientOutcome;
                    existing.DiagnosisOther = encounter.DiagnosisOther;
                    existing.ServicesProvidedOther = encounter.ServicesProvidedOther;
                    existing.MedicinesOther = encounter.MedicinesOther;
                    existing.LaboratoryOther = encounter.LaboratoryOther;
                    existing.PreventiveServicesOther = encounter.PreventiveServicesOther;
                    existing.SelectedMedicinesData = encounter.SelectedMedicinesData;
                    existing.PreventiveServicesData = encounter.PreventiveServicesData;
                    existing.ImmunizationsData = encounter.ImmunizationsData;
                    existing.ScreeningTestsData = encounter.ScreeningTestsData;
                    existing.DoctorId = encounter.DoctorId;
                    existing.ChiefComplaint = encounter.ChiefComplaint;
                    existing.Diagnosis = encounter.Diagnosis;
                    // Persist selected laboratory tests from the posted model
                    existing.LabTests = (encounter.SelectedLaboratoryTests == null || encounter.SelectedLaboratoryTests.Count == 0)
                        ? null
                        : string.Join(", ", encounter.SelectedLaboratoryTests.Select(s => s.Trim()));
                    existing.LabFee = await ResolveLaboratoryFeeAsync(existing.ProviderId, encounter.SelectedLaboratoryTests ?? Enumerable.Empty<string>());

                    // If laboratory investigations were posted with results, persist them into LabTests summary and LabFee
                    if (encounter.LaboratoryInvestigations != null && encounter.LaboratoryInvestigations.Count > 0)
                    {
                        var selectedNames = encounter.LaboratoryInvestigations
                            .Where(x => !x.IsDeleted)
                            .Select(x => x.LaboratoryTestName.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        existing.LabTests = selectedNames.Count == 0 ? existing.LabTests : string.Join(", ", selectedNames);
                        existing.LabFee = await ResolveLaboratoryFeeAsync(existing.ProviderId, selectedNames);

                        var resultLines = encounter.LaboratoryInvestigations
                            .Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.Result))
                            .Select(x => $"{x.LaboratoryTestName}: {x.Result}{(string.IsNullOrWhiteSpace(x.ResultUnit) ? string.Empty : " " + x.ResultUnit)} ({x.ResultStatus})")
                            .ToList();
                        if (resultLines.Count > 0)
                        {
                            existing.LabTests = (existing.LabTests ?? string.Empty) + (string.IsNullOrWhiteSpace(existing.LabTests) ? string.Empty : " - ") + string.Join("; ", resultLines);
                        }
                    }
                    existing.TreatmentGiven = encounter.TreatmentGiven;
                    existing.Notes = encounter.Notes;
                    // Update normalized presenting complaints
                    existing.PresentingComplaints.Clear();
                    if (encounter.SelectedPresentingComplaints != null && encounter.SelectedPresentingComplaints.Count > 0)
                    {
                        var list = encounter.SelectedPresentingComplaints
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .Select(name => new EncounterPresentingComplaint { ComplaintName = name })
                            .ToList();
                        if (!string.IsNullOrWhiteSpace(encounter.PresentingComplaintsOther)
                            && list.Any(x => string.Equals(x.ComplaintName, "Other", System.StringComparison.OrdinalIgnoreCase)))
                        {
                            var other = list.FirstOrDefault(x => string.Equals(x.ComplaintName, "Other", System.StringComparison.OrdinalIgnoreCase));
                            if (other != null)
                            {
                                other.ComplaintName = $"Other: {encounter.PresentingComplaintsOther.Trim()}";
                            }
                        }
                        foreach (var pc in list) existing.PresentingComplaints.Add(pc);
                    }
                    ApplyDoctorSnapshot(existing, attendingDoctor!);
                    _context.EncounterServices.RemoveRange(existing.Services);
                    existing.Services.Clear();
                    SetEncounterServices(existing, encounter.SelectedServices);
                    existing.DrugFee = existing.Prescriptions.Sum(prescription => prescription.TotalCost);
                    if (existing.SubmittedToHmoAt.HasValue)
                    {
                        existing.CapitationCharge = await ResolveEncounterCapitationChargeAsync(
                            existing.ProviderId,
                            existing.Enrollee?.HmoId,
                            existing.VisitDate);
                    }
                    if (!await DeductPendingEncounterPrescriptionsAsync(existing))
                    {
                        encounter.Enrollee = existing.Enrollee;
                        encounter.Doctor = existing.Doctor;
                        await PrepareEncounterFormAsync(encounter);
                        return View(encounter);
                    }

                    ApplicationUser? amendmentUser = await _userManager.GetUserAsync(User);
                    string amendmentActor = amendmentUser?.FullName ?? amendmentUser?.Email ?? User.Identity?.Name ?? "Provider";
                    existing.AuditTrails.Add(new EncounterAuditTrail
                    {
                        Action = "Facility.Amended",
                        PerformedByName = amendmentActor,
                        PerformedAt = DateTime.UtcNow,
                        Summary = "Facility amended the encounter after initial submission; original source snapshot remains preserved.",
                        NewValuesJson = BuildEncounterSourceSnapshot(existing)
                    });

                    if (existing.HmoVerificationStatus == "Query Raised")
                    {
                        existing.HmoVerificationStatus = "Submitted";
                    }

                    await _context.SaveChangesAsync();
                    ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                    await _auditService.LogAsync(
                        "Encounter.Updated",
                        AuditActor.Format(currentUser, User.Identity?.Name),
                        existing.EncounterNumber,
                        AuditActor.Details(
                            $"Enrollee:{existing.EnrolleeId}",
                            $"Provider:{existing.ProviderId}",
                            $"Status:{existing.Status}",
                            "Funding: Capitation"),
                        HttpContext.RequestAborted);
                    TempData["Success"] = $"Encounter {encounter.EncounterNumber} updated successfully!";

                   return RedirectToAction("Index", "Encounters");

                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Failed to update encounter. Please try again.";
                }
            }

            // ONLY CALL ONCE
            encounter.Enrollee = existing.Enrollee;
            encounter.Doctor = existing.Doctor;
            await PrepareEncounterFormAsync(encounter);
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
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();
            return View(encounter);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var encounter = await _context.Encounters
                .Include(e => e.Services)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (encounter != null)
            {
                if (!await CanAccessProviderAsync(encounter.ProviderId))
                {
                    return Forbid();
                }

                if (encounter.ClaimId.HasValue)
                {
                    TempData["Error"] = "A claimed encounter cannot be deleted.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                _context.EncounterServices.RemoveRange(encounter.Services);
                _context.Encounters.Remove(encounter);
                await _context.SaveChangesAsync();
                ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync(
                    "Encounter.Deleted",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    encounter.EncounterNumber,
                    AuditActor.Details(
                        $"Enrollee:{encounter.EnrolleeId}",
                        $"Provider:{encounter.ProviderId}",
                        "Funding: Capitation"),
                    HttpContext.RequestAborted);
                TempData["Success"] = $"Encounter {encounter.EncounterNumber} deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // SEARCH ENROLLEE
        [HttpGet]
        public async Task<IActionResult> PresentingComplaintsMonthly(int year, int month, int top = 10, string referral = "All")
        {
            if (year <= 0 || month < 1 || month > 12)
            {
                return BadRequest(new { success = false, error = "Invalid year or month" });
            }

            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var query = _context.EncounterPresentingComplaints
                .AsNoTracking()
                .Include(pc => pc.Encounter)
                .Where(pc => pc.Encounter != null && pc.Encounter.VisitDate >= start && pc.Encounter.VisitDate < end)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(referral) && !string.Equals(referral, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(referral, "Referred", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(pc => pc.Encounter.Status != null && pc.Encounter.Status.ToLower() == "referred" || (pc.Encounter.VisitType != null && pc.Encounter.VisitType.ToLower().Contains("referral")));
                }
                else if (string.Equals(referral, "NonReferred", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(pc => (pc.Encounter.Status == null || pc.Encounter.Status.ToLower() != "referred") && (pc.Encounter.VisitType == null || !pc.Encounter.VisitType.ToLower().Contains("referral")));
                }
            }

            var items = await query
                .GroupBy(pc => pc.ComplaintName)
                .Select(g => new { Complaint = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Complaint)
                .Take(top)
                .ToListAsync();

            return Json(new { success = true, year, month, items });
        }

        public async Task<IActionResult> SearchEnrollee(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new { success = false });

            var enrollee = await _context.Enrollees
                .Include(e => e.Hmo)
                .FirstOrDefaultAsync(e => e.EnrollmentNumber.ToUpper() == q.Trim().ToUpper());

            if (enrollee == null)
                return Json(new { success = false });

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("Provider")
                && currentUser?.ProviderId.HasValue == true
                && enrollee.ProviderId != currentUser.ProviderId.Value)
            {
                return Json(new { success = false });
            }

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
        public async Task<IActionResult> GetDoctorsByProvider(int providerId, int? selectedDoctorId = null)
        {
            if (!await CanAccessProviderAsync(providerId))
            {
                return Json(new { success = false, doctors = Array.Empty<object>() });
            }

            var doctors = await _context.Doctors
                .Where(doctor =>
                    doctor.ProviderId == providerId
                    && (doctor.IsActive || doctor.Id == selectedDoctorId))
                .OrderBy(doctor => doctor.FullName)
                .Select(doctor => new
                {
                    id = doctor.Id,
                    text = doctor.FullName + " - " + doctor.Specialty,
                    selected = selectedDoctorId.HasValue && doctor.Id == selectedDoctorId.Value
                })
                .ToListAsync();

            return Json(new { success = true, doctors });
        }

        [HttpGet]
        public async Task<IActionResult> GetDrugInventoryByProvider(int providerId)
        {
            if (!await CanAccessProviderAsync(providerId))
            {
                return Json(new { success = false, items = Array.Empty<object>() });
            }

            bool isPrimaryProvider = await _context.Providers
                .AsNoTracking()
                .AnyAsync(provider => provider.Id == providerId && provider.IsActive && provider.Level == "Primary");

            if (!isPrimaryProvider)
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            List<DrugInventoryItem> inventoryItems = await _context.DrugInventoryItems
                .AsNoTracking()
                .Where(item => item.ProviderId == providerId && item.IsActive && item.QuantityOnHand > 0)
                .OrderBy(item => item.DrugName)
                .ThenBy(item => item.Strength)
                .ToListAsync();

            var items = inventoryItems.Select(item => new
            {
                id = item.Id,
                text = FormatDrugInventoryLabel(item),
                stock = item.QuantityOnHand,
                unit = item.UnitOfMeasure,
                unitCost = item.UnitCost
            });

            return Json(new { success = true, items });
        }
        [HttpGet]
        public async Task<IActionResult> GetLaboratoryServicesByProvider(int providerId)
        {
            if (!await CanAccessProviderAsync(providerId))
            {
                return Json(new { success = false, items = Array.Empty<object>() });
            }

            List<SelectListItem> options = await BuildLaboratoryOptionsAsync(providerId, Array.Empty<string>());
            return Json(new
            {
                success = true,
                items = options.Select(item => new { name = item.Value, text = item.Text })
            });
        }


        [Authorize(Roles = "Provider")]
        public IActionResult CreateClaim(int id)
        {
            return RedirectToAction("CreateClaim", "Providers", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondEncounterQuery(int queryId, string response, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                TempData["Error"] = "Enter a response before submitting the encounter query.";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            EncounterQuery? query = await _context.EncounterQueries
                .Include(x => x.Encounter)
                .FirstOrDefaultAsync(x => x.Id == queryId, cancellationToken);

            if (query?.Encounter == null)
            {
                return NotFound();
            }

            if (!await CanAccessProviderAsync(query.Encounter.ProviderId))
            {
                return Forbid();
            }

            string actorName = currentUser?.FullName ?? currentUser?.Email ?? User.Identity?.Name ?? "Provider";
            query.Response = response.Trim();
            query.RespondedAt = DateTime.UtcNow;
            query.RespondedByName = actorName;
            query.Status = "Responded";
            query.Encounter.HmoVerificationStatus = "Submitted";

            _context.EncounterAuditTrails.Add(new EncounterAuditTrail
            {
                EncounterId = query.EncounterId,
                Action = "Facility.QueryResponded",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = $"Facility responded to query {query.QueryNumber}."
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Encounter query response submitted electronically to the HMO.";
            return User.IsInRole("Provider")
                ? RedirectToAction("ENCDetails", "Providers", new { id = query.EncounterId })
                : RedirectToAction(nameof(Details), new { id = query.EncounterId });
        }

        private async Task PrepareEncounterFormAsync(Encounter encounter)
        {
            await PopulateDropdowns(encounter.ProviderId, encounter.DoctorId, encounter.ReasonForEncounter);
            await PopulateReferralFieldsAsync(encounter);

            // Presenting complaints lookup and model mapping
            // Prefer normalized presenting complaints if available
            if (encounter.PresentingComplaints != null && encounter.PresentingComplaints.Count > 0)
            {
                encounter.SelectedPresentingComplaints = encounter.PresentingComplaints
                    .Select(pc => pc.ComplaintName)
                    .ToList();
                // extract Other: text if present
                var other = encounter.SelectedPresentingComplaints.FirstOrDefault(p => p.StartsWith("Other:", System.StringComparison.OrdinalIgnoreCase));
                if (other != null)
                {
                    var idx = other.IndexOf(':');
                    if (idx >= 0 && idx + 1 < other.Length)
                    {
                        encounter.PresentingComplaintsOther = other.Substring(idx + 1).Trim();
                    }
                }
            }
            else
            {
                // fallback to parsing ChiefComplaint free-text
                if (!string.IsNullOrWhiteSpace(encounter.ChiefComplaint)
                    && (encounter.SelectedPresentingComplaints == null || encounter.SelectedPresentingComplaints.Count == 0))
                {
                    var parts = encounter.ChiefComplaint.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim()).ToList();
                    encounter.SelectedPresentingComplaints = parts
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();
                    var other = parts.FirstOrDefault(p => p.StartsWith("Other:", System.StringComparison.OrdinalIgnoreCase));
                    if (other != null)
                    {
                        var idx = other.IndexOf(':');
                        if (idx >= 0 && idx + 1 < other.Length)
                        {
                            encounter.PresentingComplaintsOther = other.Substring(idx + 1).Trim();
                        }
                    }
                }
            }

            ViewBag.PresentingComplaints = EncounterPresentingComplaintsCatalog.BuildSelectList(encounter.SelectedPresentingComplaints);

            encounter.SelectedDiagnoses = SplitClinicalData(encounter.Diagnosis);
            encounter.SelectedMedicines = SplitClinicalData(encounter.SelectedMedicinesData);
            encounter.SelectedPreventiveServices = SplitClinicalData(encounter.PreventiveServicesData);
            encounter.SelectedImmunizations = SplitClinicalData(encounter.ImmunizationsData);
            encounter.SelectedScreeningTests = SplitClinicalData(encounter.ScreeningTestsData);

            if (!string.IsNullOrWhiteSpace(encounter.LabTests) && (encounter.SelectedLaboratoryTests == null || encounter.SelectedLaboratoryTests.Count == 0))
            {
                encounter.SelectedLaboratoryTests = encounter.LabTests
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            ViewBag.LaboratoryCatalog = await BuildLaboratoryOptionsAsync(
                encounter.ProviderId,
                encounter.SelectedLaboratoryTests);
        }

        private async Task PopulateDropdowns(int? selectedProviderId = null, int? selectedDoctorId = null, string? selectedReason = null)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            IQueryable<Provider> providerQuery = _context.Providers.Where(provider => provider.IsActive);

            if (User.IsInRole("Provider") && currentUser?.ProviderId.HasValue == true)
            {
                selectedProviderId = currentUser.ProviderId.Value;
                providerQuery = providerQuery.Where(provider => provider.Id == selectedProviderId.Value);
            }

            var providers = await providerQuery
               .OrderBy(p => p.Name)
               .ToListAsync();

            ViewBag.Providers = providers
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.State}",
                    Selected = selectedProviderId.HasValue && p.Id == selectedProviderId.Value
                })
                .ToList();

            IQueryable<Doctor> doctorQuery = _context.Doctors
                .Where(doctor => doctor.IsActive || doctor.Id == selectedDoctorId);

            if (selectedProviderId.HasValue)
            {
                doctorQuery = doctorQuery.Where(doctor => doctor.ProviderId == selectedProviderId.Value);
            }

            ViewBag.Doctors = await doctorQuery
                .OrderBy(doctor => doctor.FullName)
                .Select(doctor => new SelectListItem
                {
                    Value = doctor.Id.ToString(),
                    Text = doctor.FullName + " - " + doctor.Specialty,
                    Selected = doctor.Id == selectedDoctorId
                })
                .ToListAsync();

            ViewBag.Statuses = new List<SelectListItem>
            {
                new() { Value = "Completed", Text = "Completed", Selected = string.Equals(Request.HasFormContentType ? Request.Form[nameof(Encounter.Status)].ToString() : null, "Completed", StringComparison.OrdinalIgnoreCase) },
                new() { Value = "Pending", Text = "Pending", Selected = string.Equals(Request.HasFormContentType ? Request.Form[nameof(Encounter.Status)].ToString() : null, "Pending", StringComparison.OrdinalIgnoreCase) },
                new() { Value = "Cancelled", Text = "Cancelled", Selected = string.Equals(Request.HasFormContentType ? Request.Form[nameof(Encounter.Status)].ToString() : null, "Cancelled", StringComparison.OrdinalIgnoreCase) },
                new() { Value = "Referred", Text = "Referred", Selected = string.Equals(Request.HasFormContentType ? Request.Form[nameof(Encounter.Status)].ToString() : null, "Referred", StringComparison.OrdinalIgnoreCase) },
            };

            ViewBag.ServiceSettings = new List<SelectListItem>
            {
                new(EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Outpatient),
                new(EncounterServiceCatalog.Inpatient, EncounterServiceCatalog.Inpatient)
            };
            ViewBag.VisitTypes = EncounterLookups.TypesOfVisit.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.PatientOutcomes = EncounterLookups.PatientOutcomes.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.Diagnoses = EncounterLookups.Diagnoses;
            ViewBag.Medicines = EncounterLookups.Medicines;
            ViewBag.PreventiveServices = EncounterLookups.PreventiveServices;
            ViewBag.Immunizations = EncounterLookups.Immunizations;
            ViewBag.ScreeningTests = EncounterLookups.ScreeningTests;
            ViewBag.EncounterReasons = EncounterReasonCatalog.BuildSelectList(selectedReason);
            ViewBag.EncounterReasonExamples = EncounterReasonCatalog.Examples;
            ViewBag.OutpatientServices = EncounterLookups.ServicesProvided;
            ViewBag.InpatientServices = EncounterLookups.ServicesProvided;
        }

        private static string FormatDrugInventoryLabel(DrugInventoryItem item)
        {
            string dosage = string.Join(" ", new[] { item.Strength, item.DosageForm }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(dosage)
                ? item.DrugName
                : $"{item.DrugName} - {dosage}";
        }
        private async Task<List<SelectListItem>> BuildDrugInventoryOptionsAsync(int? providerId)
        {
            if (!providerId.HasValue)
            {
                return new List<SelectListItem>();
            }

            bool isPrimaryProvider = await _context.Providers
                .AsNoTracking()
                .AnyAsync(provider => provider.Id == providerId.Value && provider.IsActive && provider.Level == "Primary");

            if (!isPrimaryProvider)
            {
                return new List<SelectListItem>();
            }

            List<DrugInventoryItem> inventoryItems = await _context.DrugInventoryItems
                .AsNoTracking()
                .Where(item => item.ProviderId == providerId.Value && item.IsActive && item.QuantityOnHand > 0)
                .OrderBy(item => item.DrugName)
                .ThenBy(item => item.Strength)
                .ToListAsync();

            return inventoryItems
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = FormatDrugInventoryLabel(item)
                })
                .ToList();
        }
        private Task<List<SelectListItem>> BuildLaboratoryOptionsAsync(
            int? providerId,
            IEnumerable<string>? selectedNames)
        {
            HashSet<string> selected = new(
                selectedNames?.Where(name => !string.IsNullOrWhiteSpace(name)) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            List<SelectListItem> options = EncounterLookups.LaboratoryTests
                .Select(name => new SelectListItem
                {
                    Value = name,
                    Text = name,
                    Selected = selected.Contains(name)
                })
                .ToList();
            return Task.FromResult(options);
        }

        private Task<decimal> ResolveLaboratoryFeeAsync(int providerId, IEnumerable<string> selectedNames)
        {
            // Primary Provider encounters are funded through capitation, not fee-for-service billing.
            return Task.FromResult(0m);
        }


        private async Task PopulateReferralFieldsAsync(Encounter encounter)
        {
            encounter.Referral ??= new EncounterReferralInputViewModel();
            encounter.Referral.RequiresReferral = RequiresEncounterReferral(encounter);
            encounter.Referral.ReferredHospitals = await _context.ReferralHospitals
                .AsNoTracking()
                .Where(hospital => hospital.IsActive)
                .OrderBy(hospital => hospital.Name)
                .Select(hospital => new SelectListItem
                {
                    Value = hospital.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(hospital.State)
                        ? hospital.Name
                        : hospital.Name + " - " + hospital.State,
                    Selected = encounter.Referral.ReferredHospitalId.HasValue
                        && encounter.Referral.ReferredHospitalId.Value == hospital.Id
                })
                .ToListAsync();
        }

        private async Task<Doctor?> ValidateDoctorSelectionAsync(
            int providerId,
            int? doctorId,
            bool activeOnly)
        {
            if (!doctorId.HasValue)
            {
                List<Doctor> providerDoctors = await _context.Doctors
                    .Where(candidate => candidate.ProviderId == providerId && (!activeOnly || candidate.IsActive))
                    .OrderBy(candidate => candidate.FullName)
                    .Take(2)
                    .ToListAsync();

                if (providerDoctors.Count == 1)
                {
                    ModelState.Remove(nameof(Encounter.DoctorId));
                    return providerDoctors[0];
                }

                ModelState.AddModelError(nameof(Encounter.DoctorId), "Select the hospital staff who attended this encounter.");
                return null;
            }

            Doctor? doctor = await _context.Doctors.FirstOrDefaultAsync(candidate =>
                candidate.Id == doctorId.Value
                && candidate.ProviderId == providerId
                && (!activeOnly || candidate.IsActive));

            if (doctor == null)
            {
                ModelState.AddModelError(
                    nameof(Encounter.DoctorId),
                    "Select an active hospital staff member registered under this facility.");
            }

            return doctor;
        }

        private static Encounter BuildDefaultEncounter()
        {
            Encounter encounter = new()
            {
                VisitDate = TrimToSecond(DateTime.Now),
                VisitType = "New Visit",
                ServiceSetting = EncounterServiceCatalog.Outpatient,
                ReasonForEncounter = EncounterReasonCatalog.DefaultReason,
                Status = "Completed",
                PatientOutcome = "Treated"
            };
            encounter.SelectedServices.Add("General Outpatient Consultation");
            return encounter;
        }

        private void EnsureEncounterDefaults(Encounter encounter)
        {
            if (string.IsNullOrWhiteSpace(encounter.VisitType))
            {
                encounter.VisitType = "New Visit";
                ModelState.Remove(nameof(Encounter.VisitType));
            }

            if (string.IsNullOrWhiteSpace(encounter.ServiceSetting))
            {
                encounter.ServiceSetting = EncounterServiceCatalog.Outpatient;
                ModelState.Remove(nameof(Encounter.ServiceSetting));
            }

            if (string.IsNullOrWhiteSpace(encounter.Status))
            {
                encounter.Status = "Completed";
                ModelState.Remove(nameof(Encounter.Status));
            }

            if (string.IsNullOrWhiteSpace(encounter.ReasonForEncounter))
            {
                encounter.ReasonForEncounter = EncounterReasonCatalog.DefaultReason;
                ModelState.Remove(nameof(Encounter.ReasonForEncounter));
            }


        }

        private async Task<Enrollee?> ValidateEnrolleeAssignmentAsync(int enrolleeId, int providerId)
        {
            Enrollee? enrollee = await _context.Enrollees
                .AsNoTracking()
                .Include(candidate => candidate.Hmo)
                .FirstOrDefaultAsync(candidate => candidate.Id == enrolleeId);

            if (enrollee == null)
            {
                ModelState.AddModelError(nameof(Encounter.EnrolleeId), "Select a valid enrollee.");
                return null;
            }

            if (enrollee.ProviderId != providerId)
            {
                ModelState.AddModelError(nameof(Encounter.EnrolleeId), "This enrollee is not assigned to the selected facility.");
            }

            return enrollee;
        }

        private async Task<Provider?> ValidateProviderSelectionAsync(int providerId)
        {
            Provider? provider = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == providerId && candidate.IsActive);

            if (provider == null)
            {
                ModelState.AddModelError(nameof(Encounter.ProviderId), "Select an active facility.");
            }

            return provider;
        }

        private void ValidateSelectedPrescriptions(Encounter encounter)
        {
            if (!encounter.SelectedServices.Contains(EncounterServiceCatalog.PrescriptionService, StringComparer.OrdinalIgnoreCase))
            {
                encounter.SelectedPrescriptions = new List<EncounterPrescriptionInputViewModel>();
                return;
            }

            HashSet<string> allowedMedicines = new(
                EncounterLookups.Medicines.SelectMany(group => group.Value),
                StringComparer.OrdinalIgnoreCase);

            List<EncounterPrescriptionInputViewModel> normalized = new();

            foreach (EncounterPrescriptionInputViewModel prescription in encounter.SelectedPrescriptions ?? new List<EncounterPrescriptionInputViewModel>())
            {
                string selectedDrug = prescription.DrugName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(selectedDrug))
                {
                    continue;
                }

                bool isOtherMedicine = string.Equals(selectedDrug, "Other Medicines", StringComparison.OrdinalIgnoreCase);
                if (!allowedMedicines.Contains(selectedDrug))
                {
                    ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Select a medicine from the primary provider drug list.");
                    continue;
                }

                string finalDrugName = selectedDrug;
                if (isOtherMedicine)
                {
                    if (string.IsNullOrWhiteSpace(prescription.OtherDrugName))
                    {
                        ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Enter the other medicine name.");
                        continue;
                    }

                    finalDrugName = prescription.OtherDrugName.Trim();
                }

                string dosage = prescription.Strength?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(dosage))
                {
                    ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Enter the dosage for each selected drug.");
                }

                if (prescription.QuantityDispensed <= 0)
                {
                    ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Prescription quantities must be greater than zero.");
                }

                normalized.Add(new EncounterPrescriptionInputViewModel
                {
                    DrugInventoryItemId = prescription.DrugInventoryItemId,
                    DrugName = finalDrugName,
                    OtherDrugName = isOtherMedicine ? finalDrugName : null,
                    Strength = dosage,
                    QuantityDispensed = prescription.QuantityDispensed,
                    StockStatus = NormalizePrescriptionStockStatus(prescription.StockStatus)
                });
            }

            encounter.SelectedPrescriptions = normalized
                .GroupBy(prescription => new
                {
                    DrugName = prescription.DrugName ?? string.Empty,
                    Strength = prescription.Strength ?? string.Empty,
                    StockStatus = prescription.StockStatus ?? string.Empty
                })
                .Select(group => new EncounterPrescriptionInputViewModel
                {
                    DrugName = group.Key.DrugName,
                    Strength = group.Key.Strength,
                    QuantityDispensed = group.Sum(item => item.QuantityDispensed),
                    StockStatus = NormalizePrescriptionStockStatus(group.Key.StockStatus)
                })
                .ToList();
        }
        private async Task ValidateReferralInputAsync(Encounter encounter, Enrollee? enrollee)
        {
            encounter.Referral ??= new EncounterReferralInputViewModel();
            encounter.Referral.RequiresReferral = RequiresEncounterReferral(encounter);

            if (!encounter.Referral.RequiresReferral)
            {
                return;
            }

            if (enrollee?.Hmo == null)
            {
                ModelState.AddModelError(
                    nameof(Encounter.EnrolleeId),
                    "The selected enrollee must have an HMO before a referral can be submitted.");
            }

            if (!encounter.Referral.ReferredHospitalId.HasValue)
            {
                ModelState.AddModelError(
                    "Referral.ReferredHospitalId",
                    "Select the referred hospital.");
            }
            else
            {
                bool hospitalIsActive = await _context.ReferralHospitals
                    .AsNoTracking()
                    .AnyAsync(hospital =>
                        hospital.Id == encounter.Referral.ReferredHospitalId.Value
                        && hospital.IsActive);

                if (!hospitalIsActive)
                {
                    ModelState.AddModelError(
                        "Referral.ReferredHospitalId",
                        "Select an active referred hospital.");
                }
            }

            if (string.IsNullOrWhiteSpace(encounter.Referral.ReasonForReferral))
            {
                ModelState.AddModelError(
                    "Referral.ReasonForReferral",
                    "Enter the reason for referral.");
            }
        }

        private async Task ApplyPrimaryProviderCapitationSubmissionAsync(
            Encounter encounter,
            Enrollee? enrollee,
            Provider? provider,
            ApplicationUser? currentUser)
        {
            if (provider == null || !string.Equals(provider.Level, "Primary", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            encounter.SubmittedToHmoAt = DateTime.UtcNow;
            encounter.HmoVerificationStatus = "Submitted";
            encounter.IhsaVerificationStatus = "Not Ready";
            encounter.CapitationCharge = await ResolveEncounterCapitationChargeAsync(encounter.ProviderId, enrollee?.HmoId, encounter.VisitDate);
            encounter.OriginalFacilityDataJson = BuildEncounterSourceSnapshot(encounter, enrollee, provider);

            string actorName = currentUser?.FullName ?? currentUser?.Email ?? User.Identity?.Name ?? "Provider";
            encounter.AuditTrails.Add(new EncounterAuditTrail
            {
                Action = "Facility.Submitted",
                PerformedByName = actorName,
                PerformedAt = DateTime.UtcNow,
                Summary = "Primary provider encounter submitted to HMO for capitation verification.",
                NewValuesJson = encounter.OriginalFacilityDataJson
            });
        }

        private async Task<decimal> ResolveEncounterCapitationChargeAsync(int providerId, int? hmoId, DateTime visitDate)
        {
            if (!hmoId.HasValue)
            {
                return 0m;
            }

            DateTime month = new(visitDate.Year, visitDate.Month, 1);
            return await _context.CapitationPayments
                .AsNoTracking()
                .Where(payment => payment.ProviderId == providerId
                    && payment.HmoId == hmoId.Value
                    && payment.ReportingMonth <= month
                    && payment.CapitationPerEnrollee > 0)
                .OrderByDescending(payment => payment.ReportingMonth)
                .Select(payment => (decimal?)payment.CapitationPerEnrollee)
                .FirstOrDefaultAsync() ?? 0m;
        }

        private static string BuildEncounterSourceSnapshot(Encounter encounter, Enrollee? enrollee = null, Provider? provider = null)
        {
            return JsonSerializer.Serialize(new
            {
                encounter.EncounterNumber,
                encounter.EnrolleeId,
                EnrollmentNumber = enrollee?.EnrollmentNumber ?? encounter.Enrollee?.EnrollmentNumber,
                EnrolleeName = enrollee?.FullName ?? encounter.Enrollee?.FullName,
                Sex = enrollee?.Gender ?? encounter.Enrollee?.Gender,
                DateOfBirth = enrollee?.DateOfBirth ?? encounter.Enrollee?.DateOfBirth,
                State = enrollee?.State ?? encounter.Enrollee?.State,
                Lga = enrollee?.LGA ?? encounter.Enrollee?.LGA,
                Ward = enrollee?.Ward ?? encounter.Enrollee?.Ward,
                Community = enrollee?.Address ?? encounter.Enrollee?.Address,
                encounter.ProviderId,
                ProviderName = provider?.Name ?? encounter.Provider?.Name,
                ProviderCode = provider?.Code ?? encounter.Provider?.Code,
                encounter.VisitDate,
                encounter.VisitType,
                encounter.ReasonForEncounter,
                encounter.ChiefComplaint,
                encounter.Diagnosis,
                Services = encounter.SelectedServices.Count > 0 ? encounter.SelectedServices : encounter.Services.Select(service => service.ServiceName).ToList(),
                encounter.LabTests,
                Prescriptions = encounter.Prescriptions.Select(prescription => new
                {
                    prescription.DrugName,
                    prescription.Strength,
                    prescription.QuantityDispensed,
                    prescription.UnitOfMeasure
                }).ToList(),
                encounter.TreatmentGiven,
                encounter.Status,
                encounter.AttendedBy,
                encounter.SeenBy,
                encounter.CapitationCharge,
                SubmittedAt = encounter.SubmittedToHmoAt
            });
        }

        private async Task<bool> CanAccessProviderAsync(int providerId)
        {
            if (User.IsInRole("CTSHIPAdmin"))
            {
                return true;
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            return currentUser?.ProviderId == providerId;
        }

        private static void ApplyDoctorSnapshot(Encounter encounter, Doctor doctor)
        {
            encounter.DoctorId = doctor.Id;
            encounter.SeenBy = doctor.FullName;
            encounter.Rank = string.IsNullOrWhiteSpace(doctor.Designation)
                ? doctor.Specialty
                : doctor.Designation;
        }

        private static Referral BuildSubmittedReferralFromEncounter(
            Encounter encounter,
            Enrollee enrollee,
            Provider provider,
            ApplicationUser? currentUser)
        {
            string actorName = currentUser?.FullName
                ?? currentUser?.Email
                ?? "Provider";

            return new Referral
            {
                EncounterReference = LimitText(encounter.EncounterNumber, 100),
                EnrolleeNumber = LimitText(enrollee.EnrollmentNumber, 100),
                EnrolleeFullName = LimitText(enrollee.FullName, 200),
                HmoCode = LimitOptionalText(enrollee.Hmo?.RegistrationNumber, 100),
                HmoName = LimitOptionalText(enrollee.Hmo?.Name, 200),
                FromProviderId = provider.Id.ToString(),
                FromProviderName = LimitText(provider.Name, 200),
                ReferredHospitalId = encounter.Referral.ReferredHospitalId.GetValueOrDefault(),
                Diagnosis = LimitText(encounter.Diagnosis, 200, encounter.ChiefComplaint),
                ReasonForReferral = LimitText(encounter.Referral.ReasonForReferral, 1000),
                ClinicalSummary = BuildClinicalSummary(encounter),
                TreatmentGiven = LimitOptionalText(encounter.TreatmentGiven, 1000),
                InvestigationSummary = LimitOptionalText(encounter.LabTests, 1000),
                Priority = encounter.Referral.Priority,
                Status = ReferralStatus.SubmittedToHmo,
                CreatedByUserId = currentUser?.Id,
                CreatedByName = LimitOptionalText(actorName, 200),
                CreatedAt = DateTime.UtcNow,
                SubmittedByUserId = currentUser?.Id,
                SubmittedToHmoAt = DateTime.UtcNow
            };
        }

        private void AddReferralAuditLogs(Referral referral, ApplicationUser? currentUser)
        {
            string? actorName = currentUser?.FullName ?? currentUser?.Email;

            _context.ReferralAuditLogs.Add(new ReferralAuditLog
            {
                ReferralId = referral.Id,
                Action = ReferralAuditAction.Created,
                PerformedByUserId = currentUser?.Id,
                PerformedByName = actorName,
                Note = "Referral created from provider encounter."
            });

            _context.ReferralAuditLogs.Add(new ReferralAuditLog
            {
                ReferralId = referral.Id,
                Action = ReferralAuditAction.SubmittedToHmo,
                PerformedByUserId = currentUser?.Id,
                PerformedByName = actorName,
                Note = "Referral submitted to HMO from provider encounter."
            });
        }

        private static string? BuildClinicalSummary(Encounter encounter)
        {
            List<string> lines = new();

            if (!string.IsNullOrWhiteSpace(encounter.ChiefComplaint))
            {
                lines.Add("Complaint: " + encounter.ChiefComplaint.Trim());
            }

            if (!string.IsNullOrWhiteSpace(encounter.Diagnosis))
            {
                lines.Add("Diagnosis: " + encounter.Diagnosis.Trim());
            }

            IEnumerable<string> services = encounter.SelectedServices.Count > 0
                ? encounter.SelectedServices
                : encounter.Services.Select(service => service.ServiceName);
            string serviceSummary = string.Join(", ", services.Where(service => !string.IsNullOrWhiteSpace(service)));

            if (!string.IsNullOrWhiteSpace(serviceSummary))
            {
                lines.Add("Services: " + serviceSummary);
            }

            if (!string.IsNullOrWhiteSpace(encounter.Notes))
            {
                lines.Add("Notes: " + encounter.Notes.Trim());
            }

            return LimitOptionalText(string.Join(Environment.NewLine, lines), 1000);
        }

        private void ValidateSelectedServices(Encounter encounter)
        {
            encounter.SelectedServices = encounter.SelectedServices
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (encounter.SelectedServices.Count == 0)
            {
                ModelState.AddModelError(nameof(Encounter.SelectedServices), "Select at least one service delivered.");
                return;
            }

            if (encounter.SelectedServices.Any(x => !EncounterServiceCatalog.IsValid(encounter.ServiceSetting, x)))
            {
                ModelState.AddModelError(nameof(encounter.SelectedServices), "One or more services do not match the selected service setting.");
            }
        }

        private void ValidateEncounterInput(Encounter encounter)
        {
            if (string.IsNullOrWhiteSpace(encounter.ChiefComplaint))
            {
                ModelState.AddModelError(nameof(Encounter.ChiefComplaint), "Complaint is required.");
            }

            if (!EncounterReasonCatalog.IsValid(encounter.ReasonForEncounter))
            {
                ModelState.AddModelError(nameof(Encounter.ReasonForEncounter), "Select a valid reason for encounter.");
            }

            if (string.IsNullOrWhiteSpace(encounter.Diagnosis))
            {
                ModelState.AddModelError(nameof(Encounter.Diagnosis), "Diagnosis is required.");
            }

            if (string.IsNullOrWhiteSpace(encounter.TreatmentGiven))
            {
                ModelState.AddModelError(nameof(Encounter.TreatmentGiven), "Treatment given is required.");
            }

            bool pharmacySelected = encounter.SelectedServices.Contains(
                EncounterServiceCatalog.PrescriptionService,
                StringComparer.OrdinalIgnoreCase);
            bool hasPrescription = encounter.SelectedPrescriptions.Any(prescription => !string.IsNullOrWhiteSpace(prescription.DrugName));
            if (pharmacySelected && !hasPrescription)
            {
                ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Select at least one drug for Pharmacy and Medicines Dispensing Service.");
            }

            if (encounter.SelectedPreventiveServices.Contains("Immunization", StringComparer.OrdinalIgnoreCase)
                && encounter.SelectedImmunizations.Count == 0)
            {
                ModelState.AddModelError(nameof(Encounter.SelectedImmunizations), "Select an immunization type.");
            }

            if (encounter.SelectedPreventiveServices.Contains("Screening Test", StringComparer.OrdinalIgnoreCase)
                && encounter.SelectedScreeningTests.Count == 0)
            {
                ModelState.AddModelError(nameof(Encounter.SelectedScreeningTests), "Select a screening test.");
            }
            if (encounter.VisitDate > DateTime.Now)
            {
                ModelState.AddModelError(nameof(Encounter.VisitDate), "Encounter date cannot be in the future.");
            }

        }

        private Task<bool> DeductPendingEncounterPrescriptionsAsync(Encounter encounter)
        {
            foreach (EncounterPrescription prescription in encounter.Prescriptions.Where(prescription => !prescription.InventoryDeducted))
            {
                prescription.InventoryDeducted = true;
                prescription.DispensedAt = DateTime.UtcNow;
            }

            return Task.FromResult(true);
        }

        private static string NormalizePrescriptionStockStatus(string? stockStatus)
        {
            return string.Equals(stockStatus, "OutOfStock", StringComparison.OrdinalIgnoreCase)
                ? "OutOfStock"
                : "Instock";
        }
        private static void SetEncounterPrescriptions(Encounter encounter)
        {
            encounter.Prescriptions.Clear();

            foreach (EncounterPrescriptionInputViewModel prescription in encounter.SelectedPrescriptions)
            {
                if (string.IsNullOrWhiteSpace(prescription.DrugName))
                {
                    continue;
                }

                encounter.Prescriptions.Add(new EncounterPrescription
                {
                    DrugInventoryItemId = prescription.DrugInventoryItemId,
                    DrugName = prescription.DrugName.Trim(),
                    Strength = string.IsNullOrWhiteSpace(prescription.Strength) ? null : prescription.Strength.Trim(),
                    DosageForm = null,
                    UnitOfMeasure = "Unit",
                    QuantityDispensed = prescription.QuantityDispensed,
                    StockStatus = NormalizePrescriptionStockStatus(prescription.StockStatus),
                    UnitCost = 0m,
                    InventoryDeducted = true,
                    DispensedAt = DateTime.UtcNow
                });
            }

            encounter.SelectedMedicines = encounter.Prescriptions.Select(prescription => prescription.DrugName).ToList();
            encounter.SelectedMedicinesData = JoinClinicalData(encounter.SelectedMedicines);
            encounter.DrugFee = 0m;
        }
        private static bool IsCompletedStatus(string? status)
        {
            return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReferralStatus(string? status)
        {
            return string.Equals(status, "Referred", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReferralVisitType(string? visitType)
        {
            return string.Equals(visitType, "Referral", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresEncounterReferral(Encounter encounter)
        {
            return encounter.SelectedServices.Any(service => string.Equals(service, "Referral Services", StringComparison.OrdinalIgnoreCase))
                || encounter.Services.Any(service => string.Equals(service.ServiceName, "Referral Services", StringComparison.OrdinalIgnoreCase));
        }

        private static string LimitText(string? value, int maxLength, string? fallback = null)
        {
            string text = string.IsNullOrWhiteSpace(value)
                ? fallback?.Trim() ?? string.Empty
                : value.Trim();

            return text.Length <= maxLength ? text : text[..maxLength];
        }

        private static string? LimitOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value.Trim();
            return text.Length <= maxLength ? text : text[..maxLength];
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


        private void ApplyClinicalSelections(Encounter encounter)
        {
            encounter.SelectedDiagnoses = CleanSelection(encounter.SelectedDiagnoses, EncounterLookups.Diagnoses);
            encounter.Diagnosis = encounter.SelectedDiagnoses.Count == 0 ? encounter.Diagnosis : string.Join(" | ", encounter.SelectedDiagnoses);
            encounter.SelectedMedicinesData = JoinClinicalData(CleanSelection(encounter.SelectedMedicines, EncounterLookups.Medicines.SelectMany(x => x.Value)));
            encounter.SelectedPreventiveServices = CleanSelection(encounter.SelectedPreventiveServices, EncounterLookups.PreventiveServices);
            encounter.PreventiveServicesData = JoinClinicalData(encounter.SelectedPreventiveServices);
            encounter.SelectedImmunizations = encounter.SelectedPreventiveServices.Contains("Immunization", StringComparer.OrdinalIgnoreCase)
                ? CleanSelection(encounter.SelectedImmunizations, EncounterLookups.Immunizations).Take(1).ToList()
                : new List<string>();
            encounter.SelectedScreeningTests = encounter.SelectedPreventiveServices.Contains("Screening Test", StringComparer.OrdinalIgnoreCase)
                ? CleanSelection(encounter.SelectedScreeningTests, EncounterLookups.ScreeningTests).Take(1).ToList()
                : new List<string>();
            encounter.ImmunizationsData = JoinClinicalData(encounter.SelectedImmunizations);
            encounter.ScreeningTestsData = JoinClinicalData(encounter.SelectedScreeningTests);

            if (!EncounterLookups.PatientOutcomes.Contains(encounter.PatientOutcome, StringComparer.OrdinalIgnoreCase))
            {
                encounter.PatientOutcome = "Treated";
            }

            encounter.Status = string.Equals(encounter.PatientOutcome, "Referred", StringComparison.OrdinalIgnoreCase)
                ? "Referred"
                : "Completed";
        }

        private static List<string> CleanSelection(IEnumerable<string>? values, IEnumerable<string> catalog)
        {
            HashSet<string> allowed = new(catalog, StringComparer.OrdinalIgnoreCase);
            return (values ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x) && allowed.Contains(x.Trim()))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? JoinClinicalData(IEnumerable<string> values)
        {
            List<string> clean = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return clean.Count == 0 ? null : string.Join(" | ", clean);
        }

        private static List<string> SplitClinicalData(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        private static void SetEncounterServices(Encounter encounter, IEnumerable<string>? selectedServices = null)
        {
            foreach (string service in selectedServices ?? encounter.SelectedServices)
            {
                encounter.Services.Add(new EncounterService
                {
                    ServiceSetting = encounter.ServiceSetting,
                    ServiceName = service
                });
            }
        }
    }
}



