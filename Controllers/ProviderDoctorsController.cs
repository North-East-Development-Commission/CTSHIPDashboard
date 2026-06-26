using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider")]
    public class ProviderDoctorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProviderDoctorsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string search = "", string status = "all")
        {
            int? providerId = await GetCurrentProviderIdAsync();
            if (!providerId.HasValue)
            {
                TempData["Error"] = "Your account is not linked to a healthcare facility.";
                return RedirectToAction("Index", "Home");
            }

            IQueryable<Doctor> query = _context.Doctors
                .Where(doctor => doctor.ProviderId == providerId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = $"%{search.Trim()}%";
                query = query.Where(doctor =>
                    EF.Functions.Like(doctor.FullName, term)
                    || EF.Functions.Like(doctor.MedicalLicenseNumber, term)
                    || EF.Functions.Like(doctor.Specialty, term));
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(doctor => doctor.IsActive);
            }
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(doctor => !doctor.IsActive);
            }

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.ProviderName = await _context.Providers
                .Where(provider => provider.Id == providerId.Value)
                .Select(provider => provider.Name)
                .FirstOrDefaultAsync() ?? "My Facility";

            return View(await query
                .OrderByDescending(doctor => doctor.IsActive)
                .ThenBy(doctor => doctor.FullName)
                .ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            int? providerId = await GetCurrentProviderIdAsync();
            if (!providerId.HasValue)
            {
                return Forbid();
            }

            return View(new Doctor { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FullName,MedicalLicenseNumber,Specialty,Designation,Phone,Email,IsActive")] Doctor doctor)
        {
            int? providerId = await GetCurrentProviderIdAsync();
            if (!providerId.HasValue)
            {
                return Forbid();
            }

            doctor.ProviderId = providerId.Value;
            NormalizeDoctor(doctor);
            await ValidateUniqueLicenseAsync(doctor);

            if (!ModelState.IsValid)
            {
                return View(doctor);
            }

            doctor.DateAdded = DateTime.UtcNow;
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{doctor.FullName} was added to the doctor directory.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            Doctor? doctor = await FindOwnedDoctorAsync(id);
            return doctor == null ? NotFound() : View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,FullName,MedicalLicenseNumber,Specialty,Designation,Phone,Email,IsActive")] Doctor model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            Doctor? doctor = await FindOwnedDoctorAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }

            NormalizeDoctor(model);
            await ValidateUniqueLicenseAsync(model, id);

            if (!ModelState.IsValid)
            {
                model.ProviderId = doctor.ProviderId;
                model.DateAdded = doctor.DateAdded;
                return View(model);
            }

            doctor.FullName = model.FullName;
            doctor.MedicalLicenseNumber = model.MedicalLicenseNumber;
            doctor.Specialty = model.Specialty;
            doctor.Designation = model.Designation;
            doctor.Phone = model.Phone;
            doctor.Email = model.Email;
            doctor.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{doctor.FullName}'s profile was updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            Doctor? doctor = await FindOwnedDoctorAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }

            ViewBag.EncounterCount = await _context.Encounters.CountAsync(encounter => encounter.DoctorId == id);
            return View(doctor);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            Doctor? doctor = await FindOwnedDoctorAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }

            if (await _context.Encounters.AnyAsync(encounter => encounter.DoctorId == id))
            {
                TempData["Error"] = "This doctor has encounter history and cannot be deleted. Deactivate the profile instead.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{doctor.FullName} was removed from the doctor directory.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetCurrentProviderIdAsync()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            return user?.ProviderId;
        }

        private async Task<Doctor?> FindOwnedDoctorAsync(int id)
        {
            int? providerId = await GetCurrentProviderIdAsync();
            if (!providerId.HasValue)
            {
                return null;
            }

            return await _context.Doctors
                .FirstOrDefaultAsync(doctor => doctor.Id == id && doctor.ProviderId == providerId.Value);
        }

        private async Task ValidateUniqueLicenseAsync(Doctor doctor, int? excludingId = null)
        {
            int? providerId = await GetCurrentProviderIdAsync();
            if (!providerId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Your account is not linked to a healthcare facility.");
                return;
            }

            bool exists = await _context.Doctors.AnyAsync(existing =>
                existing.ProviderId == providerId.Value
                && existing.MedicalLicenseNumber == doctor.MedicalLicenseNumber
                && (!excludingId.HasValue || existing.Id != excludingId.Value));

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(Doctor.MedicalLicenseNumber),
                    "A doctor with this licence number already exists at your facility.");
            }
        }

        private static void NormalizeDoctor(Doctor doctor)
        {
            doctor.FullName = doctor.FullName?.Trim() ?? string.Empty;
            doctor.MedicalLicenseNumber = doctor.MedicalLicenseNumber?.Trim().ToUpperInvariant() ?? string.Empty;
            doctor.Specialty = doctor.Specialty?.Trim() ?? string.Empty;
            doctor.Designation = string.IsNullOrWhiteSpace(doctor.Designation) ? null : doctor.Designation.Trim();
            doctor.Phone = string.IsNullOrWhiteSpace(doctor.Phone) ? null : doctor.Phone.Trim();
            doctor.Email = string.IsNullOrWhiteSpace(doctor.Email) ? null : doctor.Email.Trim();
        }
    }
}
