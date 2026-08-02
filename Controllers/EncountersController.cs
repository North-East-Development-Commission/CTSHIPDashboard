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

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,CTSHIPAdmin")]
    public class EncountersController : Controller
    {
        private static readonly string[] EncounterReasons =
        {
            "Preventive services",
            "Acute illness",
            "Chronic disease management",
            "Maternal health",
            "Child health",
            "Reproductive health",
            "Injury/Emergency",
            "Follow-up care",
            "Administrative services",
            "Referral"
        };
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

        // INDEX — FULL LIST WITH SEARCH & FILTER
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

        // CREATE POST — CLEAN & PERFECT
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
            ValidateSelectedServices(encounter);
            ValidateEncounterInput(encounter);
            Enrollee? selectedEnrollee =
                await ValidateEnrolleeAssignmentAsync(encounter.EnrolleeId, encounter.ProviderId);
            Provider? selectedProvider = await ValidateProviderSelectionAsync(encounter.ProviderId);
            List<DrugInventoryItem> prescriptionInventoryItems = await ValidateSelectedPrescriptionsAsync(encounter, selectedProvider);
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
                    SetEncounterPrescriptions(encounter, prescriptionInventoryItems);
                    if (IsCompletedStatus(encounter.Status))
                    {
                        DeductInventoryForPrescriptions(encounter.Prescriptions, prescriptionInventoryItems);
                    }

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
                            $"Total:NGN {encounter.TotalAmount:N2}"));

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

        // EDIT GET — CLEAN
        public async Task<IActionResult> Edit(int id)
        {
            var encounter = await _context.Encounters
                .Include(x => x.Enrollee).ThenInclude(x => x!.Hmo)
                .Include(x => x.Doctor)
                .Include(x => x.Services)
                .Include(x => x.Prescriptions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (encounter == null) return NotFound();
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();

            encounter.VisitDate = TrimToSecond(encounter.VisitDate);
            encounter.SelectedServices = encounter.Services.Select(x => x.ServiceName).ToList();
            encounter.SelectedPrescriptions = encounter.Prescriptions
                .Select(prescription => new EncounterPrescriptionInputViewModel
                {
                    DrugInventoryItemId = prescription.DrugInventoryItemId,
                    QuantityDispensed = prescription.QuantityDispensed
                })
                .ToList();
            await PrepareEncounterFormAsync(encounter);
            return View(encounter);
        }

        // EDIT POST — CLEAN & SAFE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Encounter encounter)
        {
            if (id != encounter.Id) return NotFound();

            EnsureEncounterDefaults(encounter);
            ValidateSelectedServices(encounter);
            ValidateEncounterInput(encounter);

            Encounter? existing = await _context.Encounters
                .Include(x => x.Enrollee)
                    .ThenInclude(x => x!.Hmo)
                .Include(x => x.Doctor)
                .Include(x => x.Services)
                .Include(x => x.Prescriptions)
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
                    existing.VisitType = encounter.VisitType;
                    existing.ServiceSetting = encounter.ServiceSetting;
                    existing.ReasonForEncounter = encounter.ReasonForEncounter;
                    existing.Status = encounter.Status;
                    existing.DoctorId = encounter.DoctorId;
                    existing.ChiefComplaint = encounter.ChiefComplaint;
                    existing.Diagnosis = encounter.Diagnosis;
                    existing.LabTests = encounter.LabTests;
                    existing.TreatmentGiven = encounter.TreatmentGiven;
                    existing.Notes = encounter.Notes;
                    ApplyDoctorSnapshot(existing, attendingDoctor!);
                    _context.EncounterServices.RemoveRange(existing.Services);
                    existing.Services.Clear();
                    SetEncounterServices(existing, encounter.SelectedServices);
                    if (!await DeductPendingEncounterPrescriptionsAsync(existing))
                    {
                        encounter.Enrollee = existing.Enrollee;
                        encounter.Doctor = existing.Doctor;
                        await PrepareEncounterFormAsync(encounter);
                        return View(encounter);
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
                            $"Total:NGN {existing.TotalAmount:N2}"),
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
                        $"Total:NGN {encounter.TotalAmount:N2}"),
                    HttpContext.RequestAborted);
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
                text = item.DisplayName + " (Stock: " + item.QuantityOnHand + " " + item.UnitOfMeasure + ")",
                stock = item.QuantityOnHand,
                unit = item.UnitOfMeasure,
                unitCost = item.UnitCost
            });

            return Json(new { success = true, items });
        }

        [Authorize(Roles = "Provider")]
        public IActionResult CreateClaim(int id)
        {
            return RedirectToAction("CreateClaim", "Providers", new { id });
        }

        private async Task PrepareEncounterFormAsync(Encounter encounter)
        {
            await PopulateDropdowns(encounter.ProviderId, encounter.DoctorId, encounter.ReasonForEncounter);
            await PopulateReferralFieldsAsync(encounter);
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
                new() { Value = "Claimed", Text = "Claimed", Selected = string.Equals(Request.HasFormContentType ? Request.Form[nameof(Encounter.Status)].ToString() : null, "Claimed", StringComparison.OrdinalIgnoreCase) }
            };

            ViewBag.ServiceSettings = new List<SelectListItem>
            {
                new(EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Outpatient),
                new(EncounterServiceCatalog.Inpatient, EncounterServiceCatalog.Inpatient)
            };
            ViewBag.VisitTypes = new List<SelectListItem>
            {
                new("New Visit", "New Visit"),
                new("Follow-up", "Follow-up"),
                new("Emergency", "Emergency"),
                new("Referral", "Referral")
            };
            ViewBag.EncounterReasons = BuildEncounterReasonOptions(selectedReason);
            ViewBag.DrugInventoryItems = await BuildDrugInventoryOptionsAsync(selectedProviderId);
            ViewBag.OutpatientServices = EncounterServiceCatalog.OutpatientServices;
            ViewBag.InpatientServices = EncounterServiceCatalog.InpatientServices;
        }

        private static List<SelectListItem> BuildEncounterReasonOptions(string? selectedReason)
        {
            return EncounterReasons
                .Select(reason => new SelectListItem
                {
                    Value = reason,
                    Text = reason,
                    Selected = string.Equals(reason, selectedReason, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
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
                    Text = item.DisplayName + " (Stock: " + item.QuantityOnHand + " " + item.UnitOfMeasure + ")"
                })
                .ToList();
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
                ReasonForEncounter = "Acute illness",
                Status = "Completed"
            };
            encounter.SelectedServices.Add("Management of common infectious diseases");
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
                encounter.ReasonForEncounter = "Acute illness";
                ModelState.Remove(nameof(Encounter.ReasonForEncounter));
            }

            if (IsReferralVisitType(encounter.VisitType)
                && !IsReferralStatus(encounter.Status))
            {
                encounter.Status = "Referred";
                ModelState.Remove(nameof(Encounter.Status));
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

        private async Task<List<DrugInventoryItem>> ValidateSelectedPrescriptionsAsync(Encounter encounter, Provider? provider)
        {
            encounter.SelectedPrescriptions = encounter.SelectedPrescriptions
                .Where(prescription => prescription.DrugInventoryItemId > 0)
                .GroupBy(prescription => prescription.DrugInventoryItemId)
                .Select(group => new EncounterPrescriptionInputViewModel
                {
                    DrugInventoryItemId = group.Key,
                    QuantityDispensed = group.Sum(item => item.QuantityDispensed)
                })
                .ToList();

            if (encounter.SelectedPrescriptions.Count == 0)
            {
                return new List<DrugInventoryItem>();
            }

            if (provider == null)
            {
                ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Select a facility before adding prescriptions.");
                return new List<DrugInventoryItem>();
            }

            if (!string.Equals(provider.Level, "Primary", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Drug inventory prescriptions are available only for primary providers.");
                return new List<DrugInventoryItem>();
            }

            foreach (EncounterPrescriptionInputViewModel prescription in encounter.SelectedPrescriptions)
            {
                if (prescription.QuantityDispensed <= 0)
                {
                    ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Prescription quantities must be greater than zero.");
                }
            }

            List<int> inventoryIds = encounter.SelectedPrescriptions
                .Select(prescription => prescription.DrugInventoryItemId)
                .Distinct()
                .ToList();

            List<DrugInventoryItem> inventoryItems = await _context.DrugInventoryItems
                .Where(item => inventoryIds.Contains(item.Id) && item.ProviderId == provider.Id && item.IsActive)
                .ToListAsync();

            foreach (EncounterPrescriptionInputViewModel prescription in encounter.SelectedPrescriptions)
            {
                DrugInventoryItem? item = inventoryItems.FirstOrDefault(candidate => candidate.Id == prescription.DrugInventoryItemId);
                if (item == null)
                {
                    ModelState.AddModelError(nameof(Encounter.SelectedPrescriptions), "Select an active drug from the provider inventory.");
                    continue;
                }

                if (item.QuantityOnHand < prescription.QuantityDispensed)
                {
                    ModelState.AddModelError(
                        nameof(Encounter.SelectedPrescriptions),
                        $"{item.DisplayName} has only {item.QuantityOnHand:N0} {item.UnitOfMeasure} in stock.");
                }
            }

            return inventoryItems;
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

            if (string.IsNullOrWhiteSpace(encounter.ReasonForEncounter)
                || !EncounterReasons.Contains(encounter.ReasonForEncounter.Trim(), StringComparer.OrdinalIgnoreCase))
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

            if (encounter.VisitDate > DateTime.Now)
            {
                ModelState.AddModelError(nameof(Encounter.VisitDate), "Encounter date cannot be in the future.");
            }

        }

        private async Task<bool> DeductPendingEncounterPrescriptionsAsync(Encounter encounter)
        {
            if (!IsCompletedStatus(encounter.Status))
            {
                return true;
            }

            List<EncounterPrescription> pendingPrescriptions = encounter.Prescriptions
                .Where(prescription => !prescription.InventoryDeducted)
                .ToList();

            if (pendingPrescriptions.Count == 0)
            {
                return true;
            }

            List<int> inventoryIds = pendingPrescriptions
                .Select(prescription => prescription.DrugInventoryItemId)
                .Distinct()
                .ToList();

            List<DrugInventoryItem> inventoryItems = await _context.DrugInventoryItems
                .Where(item => inventoryIds.Contains(item.Id) && item.ProviderId == encounter.ProviderId && item.IsActive)
                .ToListAsync();

            foreach (EncounterPrescription prescription in pendingPrescriptions)
            {
                DrugInventoryItem? item = inventoryItems.FirstOrDefault(candidate => candidate.Id == prescription.DrugInventoryItemId);
                if (item == null)
                {
                    ModelState.AddModelError(string.Empty, $"{prescription.DrugName} is no longer active in inventory.");
                    return false;
                }

                if (item.QuantityOnHand < prescription.QuantityDispensed)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"{item.DisplayName} has only {item.QuantityOnHand:N0} {item.UnitOfMeasure} in stock.");
                    return false;
                }
            }

            DeductInventoryForPrescriptions(pendingPrescriptions, inventoryItems);
            return true;
        }

        private static void SetEncounterPrescriptions(Encounter encounter, IReadOnlyCollection<DrugInventoryItem> inventoryItems)
        {
            foreach (EncounterPrescriptionInputViewModel prescription in encounter.SelectedPrescriptions)
            {
                DrugInventoryItem? item = inventoryItems.FirstOrDefault(candidate => candidate.Id == prescription.DrugInventoryItemId);
                if (item == null)
                {
                    continue;
                }

                encounter.Prescriptions.Add(new EncounterPrescription
                {
                    DrugInventoryItemId = item.Id,
                    DrugName = item.DrugName,
                    Strength = item.Strength,
                    DosageForm = item.DosageForm,
                    UnitOfMeasure = item.UnitOfMeasure,
                    QuantityDispensed = prescription.QuantityDispensed,
                    UnitCost = item.UnitCost,
                    InventoryDeducted = false,
                    DispensedAt = DateTime.UtcNow
                });
            }
        }

        private static void DeductInventoryForPrescriptions(
            IEnumerable<EncounterPrescription> prescriptions,
            IReadOnlyCollection<DrugInventoryItem> inventoryItems)
        {
            foreach (EncounterPrescription prescription in prescriptions.Where(prescription => !prescription.InventoryDeducted))
            {
                DrugInventoryItem? item = inventoryItems.FirstOrDefault(candidate => candidate.Id == prescription.DrugInventoryItemId);
                if (item == null)
                {
                    continue;
                }

                item.QuantityOnHand -= prescription.QuantityDispensed;
                item.UpdatedAt = DateTime.UtcNow;
                prescription.InventoryDeducted = true;
                prescription.DispensedAt = DateTime.UtcNow;
            }
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
            return IsReferralStatus(encounter.Status) || IsReferralVisitType(encounter.VisitType);
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
