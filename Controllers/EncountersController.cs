using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,Admin")]
    public class EncountersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AnalyticsHub> _hubContext;
        private readonly CTSHIPDashboard.Services.IAuditService _auditService;

        public EncountersController(
            ApplicationDbContext context,
            IHubContext<AnalyticsHub> hubContext,
            UserManager<ApplicationUser> userManager,
            CTSHIPDashboard.Services.IAuditService auditService)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
            _auditService = auditService;
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
                .FirstOrDefaultAsync(e => e.Id == id);
            if (encounter == null) return NotFound();
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();
            return View(encounter);
        }

        // CREATE GET
        public async Task<IActionResult> Create()
        {
            Encounter model = new();
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

            await PopulateDropdowns(model.ProviderId, model.DoctorId);
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

            ValidateSelectedServices(encounter);
            Doctor? attendingDoctor = await ValidateDoctorSelectionAsync(
                encounter.ProviderId,
                encounter.DoctorId,
                activeOnly: true);

            if (encounter.TotalAmount <= 0)
            {
                ModelState.AddModelError(string.Empty, "The total encounter amount must be greater than zero.");
            }

            if (ModelState.IsValid)
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var wallet = await _context.EnrolleeWallets
                        .FirstOrDefaultAsync(w => w.EnrolleeId == encounter.EnrolleeId);

                    if (wallet == null || wallet.Balance < encounter.TotalAmount)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError(
                            string.Empty,
                            $"Insufficient wallet balance. Encounter total: {encounter.TotalAmount:C}; available balance: {(wallet?.Balance ?? 0):C}.");
                        await PopulateDropdowns(encounter.ProviderId, encounter.DoctorId);
                        return View(encounter);
                    }

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
                    encounter.Status = "Completed";
                    encounter.IsBilled = false;

                    SetEncounterServices(encounter);
                    _context.Encounters.Add(encounter);

                    wallet.Balance -= encounter.TotalAmount;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        EnrolleeWallet = wallet,
                        Amount = -encounter.TotalAmount,
                        Type = "Deduction",
                        Reference = $"Encounter {encounter.EncounterNumber}",
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var actor = currentUser?.Email ?? User.Identity?.Name ?? "System";
                    await _auditService.LogAsync(
                        "EncounterDeduction",
                        actor,
                        encounter.EnrolleeId.ToString(),
                        $"Encounter {encounter.EncounterNumber}; deducted total {encounter.TotalAmount:C}; new balance {wallet.Balance:C}.");

                    TempData["Success"] =
                        $"Encounter {encounter.EncounterNumber} recorded. {encounter.TotalAmount:C} was deducted from the enrollee wallet.";

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
                        "The encounter and wallet deduction could not be completed. No funds were deducted.");
                }
            }

            await PopulateDropdowns(encounter.ProviderId, encounter.DoctorId);
            return View(encounter);
        }

        // EDIT GET — CLEAN
        public async Task<IActionResult> Edit(int id)
        {
            var encounter = await _context.Encounters
                .Include(x => x.Enrollee).ThenInclude(x => x!.Hmo)
                .Include(x => x.Doctor)
                .Include(x => x.Services)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (encounter == null) return NotFound();
            if (!await CanAccessProviderAsync(encounter.ProviderId)) return Forbid();

            encounter.SelectedServices = encounter.Services.Select(x => x.ServiceName).ToList();
            await PopulateDropdowns(encounter.ProviderId, encounter.DoctorId);
            return View(encounter);
        }

        // EDIT POST — CLEAN & SAFE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Encounter encounter)
        {
            if (id != encounter.Id) return NotFound();

            ValidateSelectedServices(encounter);

            Encounter? existing = await _context.Encounters
                .Include(x => x.Services)
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
                    existing.VisitType = encounter.VisitType;
                    existing.ServiceSetting = encounter.ServiceSetting;
                    existing.Status = encounter.Status;
                    existing.DoctorId = encounter.DoctorId;
                    existing.Temperature = encounter.Temperature;
                    existing.BloodPressure = encounter.BloodPressure;
                    existing.PulseRate = encounter.PulseRate;
                    existing.ChiefComplaint = encounter.ChiefComplaint;
                    existing.Diagnosis = encounter.Diagnosis;
                    existing.LabTests = encounter.LabTests;
                    existing.TreatmentGiven = encounter.TreatmentGiven;
                    existing.ConsultationFee = encounter.ConsultationFee;
                    existing.LabFee = encounter.LabFee;
                    existing.DrugFee = encounter.DrugFee;
                    existing.Notes = encounter.Notes;
                    ApplyDoctorSnapshot(existing, attendingDoctor!);
                    _context.EncounterServices.RemoveRange(existing.Services);
                    existing.Services.Clear();
                    SetEncounterServices(existing, encounter.SelectedServices);
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
            await PopulateDropdowns(existing.ProviderId, encounter.DoctorId);
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
            var encounter = await _context.Encounters.FindAsync(id);
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

                var services = await _context.EncounterServices
                    .Where(x => x.EncounterId == id)
                    .ToListAsync();
                _context.EncounterServices.RemoveRange(services);
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

            if (!await CanAccessProviderAsync(encounter.ProviderId))
            {
                return Forbid();
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
            // Audit claim creation
            try
            {
                var actor = User.Identity?.Name ?? "Unknown";
                await _auditService.LogAsync("ClaimCreated", actor, encounter.Enrollee?.EnrollmentNumber, $"Claim:{claim.ClaimNumber}; Amount:{claim.Amount:C}; Encounter:{encounter.EncounterNumber}");
            }
            catch { }
            return RedirectToAction("Index", "Claims");
        }

        private async Task PopulateDropdowns(int? selectedProviderId = null, int? selectedDoctorId = null)
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
                    Text = $"{p.Name} - {p.State}"
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
                    Text = doctor.FullName + " — " + doctor.Specialty,
                    Selected = doctor.Id == selectedDoctorId
                })
                .ToListAsync();

            ViewBag.Statuses = new List<SelectListItem>
            {
                new() { Value = "Completed", Text = "Completed" },
                new() { Value = "Pending", Text = "Pending" },
                new() { Value = "Cancelled", Text = "Cancelled" },
                new() { Value = "Referred", Text = "Referred" },
                new() { Value = "Claimed", Text = "Claimed" }
            };

            ViewBag.ServiceSettings = new List<SelectListItem>
            {
                new(EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Outpatient),
                new(EncounterServiceCatalog.Inpatient, EncounterServiceCatalog.Inpatient)
            };
            ViewBag.OutpatientServices = EncounterServiceCatalog.OutpatientServices;
            ViewBag.InpatientServices = EncounterServiceCatalog.InpatientServices;
        }

        private async Task<Doctor?> ValidateDoctorSelectionAsync(
            int providerId,
            int? doctorId,
            bool activeOnly)
        {
            if (!doctorId.HasValue)
            {
                ModelState.AddModelError(nameof(Encounter.DoctorId), "Select the attending doctor.");
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
                    "Select an active doctor registered under this facility.");
            }

            return doctor;
        }

        private async Task<bool> CanAccessProviderAsync(int providerId)
        {
            if (User.IsInRole("Admin"))
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

        private void ValidateSelectedServices(Encounter encounter)
        {
            encounter.SelectedServices = encounter.SelectedServices
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (encounter.SelectedServices.Count == 0)
            {
                ModelState.AddModelError(nameof(encounter.SelectedServices), "Select at least one service delivered.");
                return;
            }

            if (encounter.SelectedServices.Any(x => !EncounterServiceCatalog.IsValid(encounter.ServiceSetting, x)))
            {
                ModelState.AddModelError(nameof(encounter.SelectedServices), "One or more services do not match the selected service setting.");
            }
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
