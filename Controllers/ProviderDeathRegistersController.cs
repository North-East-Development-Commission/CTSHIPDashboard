using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,Admin")]
    [Route("Providers/DeathRegisters")]
    public class ProviderDeathRegistersController : Controller
    {
        private readonly IDeathRegisterService _deathRegisterService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProviderDeathRegistersController(
            IDeathRegisterService deathRegisterService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _deathRegisterService = deathRegisterService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
        {
            bool isAdmin = User.IsInRole("Admin");
            Provider? provider = isAdmin ? null : await GetCurrentProviderAsync(cancellationToken);

            if (!isAdmin && provider == null)
            {
                TempData["ErrorMessage"] = "Your account is not linked to a healthcare facility.";
                return View(new List<DeathRegisterIndexViewModel>());
            }

            List<DeathRegisterIndexViewModel> deathRegisters =
                await _deathRegisterService.GetProviderDeathRegistersAsync(
                    isAdmin ? null : provider!.Id.ToString(),
                    isAdmin ? null : provider!.Code,
                    search,
                    cancellationToken);

            ViewBag.Search = search;
            return View(deathRegisters);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create(
            int? enrolleeId,
            string? enrolleeNumber,
            string? enrolleeFullName,
            string? hmoCode,
            string? hmoName,
            CancellationToken cancellationToken)
        {
            DeathRegisterCreateViewModel model =
                await _deathRegisterService.BuildCreateViewModelAsync(null, cancellationToken);
            Enrollee? enrollee =
                await FindAccessibleEnrolleeAsync(enrolleeId, enrolleeNumber, cancellationToken);

            if (enrollee == null)
            {
                TempData["ErrorMessage"] = "Select an enrollee assigned to your facility before registering a death.";
                return RedirectToAction(nameof(Index));
            }

            ApplyEnrolleeData(model, enrollee);
            return View(model);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            DeathRegisterCreateViewModel model,
            string submitAction,
            CancellationToken cancellationToken)
        {
            bool submitToHmo =
                string.Equals(submitAction, "submit", StringComparison.OrdinalIgnoreCase);
            bool saveDraft =
                string.Equals(submitAction, "draft", StringComparison.OrdinalIgnoreCase);

            Enrollee? enrollee =
                await FindAccessibleEnrolleeAsync(model.EnrolleeId, model.EnrolleeNumber, cancellationToken);

            if (enrollee == null)
            {
                ModelState.AddModelError(
                    nameof(model.EnrolleeNumber),
                    "The enrollee was not found under your facility.");
            }
            else
            {
                ApplyEnrolleeData(model, enrollee);
                ModelState.Clear();
                TryValidateModel(model);

                bool existingRecord = await _context.DeathRegisters
                    .AsNoTracking()
                    .AnyAsync(record =>
                        !record.IsDeleted
                        && (record.EnrolleeId == enrollee.Id
                            || record.EnrolleeNumber == enrollee.EnrollmentNumber)
                        && record.Status != DeathRegisterStatus.Cancelled
                        && record.Status != DeathRegisterStatus.HmoRejected
                        && record.Status != DeathRegisterStatus.AuditRejected,
                        cancellationToken);

                if (existingRecord)
                {
                    ModelState.AddModelError(
                        nameof(model.EnrolleeNumber),
                        "An active death register already exists for this enrollee.");
                }
            }

            if (!submitToHmo && !saveDraft)
            {
                ModelState.AddModelError(string.Empty, "Select Save Draft or Submit to HMO.");
            }

            if (submitToHmo && string.IsNullOrWhiteSpace(model.HmoCode))
            {
                ModelState.AddModelError(
                    nameof(model.HmoCode),
                    "Assign an HMO before submitting the death register.");
            }

            if (!ModelState.IsValid)
            {
                DeathRegisterCreateViewModel invalidModel =
                    await _deathRegisterService.BuildCreateViewModelAsync(model, cancellationToken);
                return View(invalidModel);
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? userName = User.Identity?.Name;
            Guid deathRegisterId = await _deathRegisterService.CreateDeathRegisterAsync(
                model,
                userId,
                userName,
                submitToHmo,
                cancellationToken);

            TempData["SuccessMessage"] = submitToHmo
                ? "Death register created and submitted to HMO."
                : "Death register saved as draft.";
            return RedirectToAction(nameof(Details), new { id = deathRegisterId });
        }

        [HttpGet("Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            DeathRegisterDetailsViewModel? deathRegister =
                await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);

            if (deathRegister == null
                || !await CanAccessDeathRegisterAsync(deathRegister, cancellationToken))
            {
                return NotFound();
            }

            return View(deathRegister);
        }

        [HttpPost("Submit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        {
            DeathRegisterDetailsViewModel? deathRegister =
                await _deathRegisterService.GetDeathRegisterDetailsAsync(id, cancellationToken);

            if (deathRegister == null
                || !await CanAccessDeathRegisterAsync(deathRegister, cancellationToken))
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(deathRegister.HmoCode))
            {
                TempData["ErrorMessage"] = "Assign an HMO before submitting the death register.";
                return RedirectToAction(nameof(Details), new { id });
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? userName = User.Identity?.Name;
            bool submitted = await _deathRegisterService.SubmitDeathRegisterToHmoAsync(
                id,
                userId,
                userName,
                cancellationToken);

            TempData[submitted ? "SuccessMessage" : "ErrorMessage"] = submitted
                ? "Death register submitted to HMO for verification."
                : "Death register could not be submitted. Only draft records can be submitted.";

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<Provider?> GetCurrentProviderAsync(CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user?.ProviderId == null)
            {
                return null;
            }

            return await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    provider => provider.Id == user.ProviderId.Value,
                    cancellationToken);
        }

        private async Task<Enrollee?> FindAccessibleEnrolleeAsync(
            int? enrolleeId,
            string? enrolleeNumber,
            CancellationToken cancellationToken)
        {
            IQueryable<Enrollee> query = _context.Enrollees
                .AsNoTracking()
                .Include(enrollee => enrollee.Hmo)
                .Include(enrollee => enrollee.provider);

            if (User.IsInRole("Provider"))
            {
                Provider? provider = await GetCurrentProviderAsync(cancellationToken);
                if (provider == null)
                {
                    return null;
                }

                query = query.Where(enrollee => enrollee.ProviderId == provider.Id);
            }

            if (enrolleeId.HasValue && enrolleeId.Value > 0)
            {
                return await query.FirstOrDefaultAsync(
                    enrollee => enrollee.Id == enrolleeId.Value,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(enrolleeNumber))
            {
                string normalizedNumber = enrolleeNumber.Trim();
                return await query.FirstOrDefaultAsync(
                    enrollee => enrollee.EnrollmentNumber == normalizedNumber,
                    cancellationToken);
            }

            return null;
        }

        private async Task<bool> CanAccessDeathRegisterAsync(
            DeathRegisterDetailsViewModel deathRegister,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            Provider? provider = await GetCurrentProviderAsync(cancellationToken);
            return provider != null
                && (string.Equals(
                        deathRegister.ProviderId,
                        provider.Id.ToString(),
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        deathRegister.ProviderId,
                        provider.Code,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static void ApplyEnrolleeData(
            DeathRegisterCreateViewModel model,
            Enrollee enrollee)
        {
            model.EnrolleeId = enrollee.Id;
            model.EnrolleeNumber = enrollee.EnrollmentNumber;
            model.EnrolleeFullName = enrollee.FullName;
            model.Gender = enrollee.Gender;
            model.DateOfBirth = enrollee.DateOfBirth;
            model.PhoneNumber = enrollee.Phone;
            model.Address = enrollee.Address;
            model.HmoCode = enrollee.Hmo?.RegistrationNumber;
            model.HmoName = enrollee.Hmo?.Name;
            model.ProviderId = enrollee.ProviderId?.ToString();
            model.ProviderName = enrollee.provider?.Name ?? "Unassigned Provider";
        }

    }
}
