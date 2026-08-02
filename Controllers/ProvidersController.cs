using AspNetCoreGeneratedDocument;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using CTSHIPDashboard.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

public class ProvidersController : Controller
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
    private const long MaxClaimEvidenceFileBytes = 10 * 1024 * 1024;
    private const int MaxClaimEvidenceFileCount = 10;

    private static readonly HashSet<string> AllowedClaimEvidenceFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".doc",
        ".docx"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppNotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;

    public ProvidersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAppNotificationService notificationService,
        IAuditService auditService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _auditService = auditService;
        _environment = environment;
    }

    // GET: Provider/Index
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Monitoring,SSHIA,NHIA,IHSA")]
    public async Task<IActionResult> Index(
        string search = "",
        string state = "",
        string level = "",
        string status = "",
        int page = 1,
        int pageSize = 10)
    {
        var providers = _context.Providers
            .Include(p => p.Enrollees)
            .Include(p => p.Encounters)
            .Include(p => p.Claims)
            .AsQueryable();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? restrictedHmoId = null;
        if (IsHmoOnlyUser())
        {
            restrictedHmoId = await GetCurrentHmoIdAsync();
            if (!restrictedHmoId.HasValue)
            {
                TempData["Error"] = "Your account is not linked to any HMO.";
                return RedirectToAction("Index", "Home");
            }

            providers = providers.Where(p => p.HmoId == restrictedHmoId.Value);
        }

        string? restrictedState = null;
        if (User.IsInRole("SSHIA"))
        {
            restrictedState = currentUser?.State?.Trim();
            if (string.IsNullOrWhiteSpace(restrictedState))
            {
                TempData["Error"] = "Your account is not linked to a state.";
                return RedirectToAction("Index", "Home");
            }

            providers = providers.Where(p => p.State == restrictedState);
            state = restrictedState;
        }

        var scopedProviders = providers;

        // SEARCH
        if (!string.IsNullOrEmpty(search))
        {
            search = search.Trim();

            providers = providers.Where(p =>
                // Use simple Contains() which translates to SQL LIKE
                p.Name.Contains(search) ||
                p.Code.Contains(search)
            );


        }

        // FILTERS
        if (!string.IsNullOrEmpty(state) && state != "all")
            providers = providers.Where(p => p.State == state);

        if (!string.IsNullOrEmpty(level) && level != "all")
            providers = providers.Where(p => p.Level == level);

        if (!string.IsNullOrEmpty(status) && status != "all")
            providers = providers.Where(p => p.IsActive == (status == "active"));

        // STATISTICS FOR HEADER
        ViewBag.TotalProviders = await scopedProviders.CountAsync();
        ViewBag.ActiveProviders = await scopedProviders.CountAsync(p => p.IsActive);
        ViewBag.TotalEnrollees = !string.IsNullOrWhiteSpace(restrictedState)
            ? await _context.Enrollees.CountAsync(e => e.State == restrictedState)
            : await _context.Enrollees.CountAsync();
        ViewBag.TotalEncounters = !string.IsNullOrWhiteSpace(restrictedState)
            ? await _context.Encounters.CountAsync(e => e.Enrollee != null && e.Enrollee.State == restrictedState)
            : await _context.Encounters.CountAsync();

        // Death stats
        var totalDeaths = await _context.DeathRegisters.CountAsync(d => !d.IsDeleted && d.Status == DeathRegisterStatus.Audited);
        ViewBag.DeathCount = totalDeaths;
        ViewBag.DeathRatePerThousand = (ViewBag.TotalEnrollees > 0) ? Math.Round((double)totalDeaths / (double)ViewBag.TotalEnrollees * 1000.0, 2) : 0;

        // PAGINATION
        var totalRecords = await providers.CountAsync();
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

        var model = await providers
            .OrderBy(p => p.State)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProviderListViewModel
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                State = p.State,
                Level = p.Level,
                IsActive = p.IsActive,
                EnrolleeCount = _context.Enrollees.Count(e => e.ProviderId == p.Id),
                EncounterCount = p.Encounters.Count,
                ClaimCount = p.Claims.Count,
                TotalRevenue = p.Claims.Where(c => c.Status == "Paid").Sum(c => c.Amount),
                DateRegistered = DateTime.Now
            })
            .ToListAsync();

        // ViewBag for UI
        ViewBag.Search = search;
        ViewBag.State = state;
        ViewBag.Level = level;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        ViewBag.States = !string.IsNullOrWhiteSpace(restrictedState)
            ? new List<SelectListItem> { new() { Value = restrictedState, Text = restrictedState, Selected = true } }
            : GetNigerianStatesWithAll(state);
        ViewBag.Levels = new List<SelectListItem>
        {
            new() { Value = "all", Text = "All Levels" },
            new() { Value = "Tertiary", Text = "Tertiary (Teaching Hospitals)" },
            new() { Value = "Secondary", Text = "Secondary (General Hospitals)" },
            new() { Value = "Private", Text = "Private Hospitals" },
            new() { Value = "Primary", Text = "Primary Health Centres (PHC)" }
        };

        return View(model);
    }

    private List<SelectListItem> GetNigerianStatesWithAll(string? selectedState = null)
    {
        return StateSelectListHelper.NorthEastStatesWithAll(selectedState);
    }


    // GET: Provider/Create
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> Create()
    {
        var provider = new Provider();
        if (IsHmoOnlyUser())
        {
            int? hmoId = await GetCurrentHmoIdAsync();
            if (!hmoId.HasValue)
            {
                TempData["Error"] = "Your account is not linked to any HMO.";
                return RedirectToAction("Index", "Home");
            }

            provider.HmoId = hmoId.Value;
        }

        await PopulateProviderFormDropdownsAsync(selectedHmoId: provider.HmoId);
        return View(provider);
    }

    // POST: Provider/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> Create(Provider provider)
    {
        ModelState.Remove(nameof(Provider.Code));

        NormalizeAndValidateProviderState(provider);
        await ApplyAndValidateProviderHmoAsync(provider);

        if (ModelState.IsValid)
        {
            try
            {
                provider.Code = await GenerateProviderCodeAsync(provider);
                provider.DateRegistered = DateTime.UtcNow;
                provider.IsActive = true;

                _context.Providers.Add(provider);
                await _context.SaveChangesAsync();

                ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync(
                    "Provider.Created",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    provider.Code,
                    AuditActor.Details(
                        $"Name:{provider.Name}",
                        $"HMO:{provider.HmoId}",
                        $"State:{provider.State}",
                        $"Level:{provider.Level}"),
                    HttpContext.RequestAborted);

                TempData["Success"] = $"Provider '{provider.Name}' has been accredited successfully with code: <strong>{provider.Code}</strong>";
                if (IsHmoOnlyUser())
                {
                    return RedirectToAction("MyProviders", "Hmo");
                }
                else if (IsProviderAdmin())
                {
                    return RedirectToAction(nameof(Index));
                }
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save provider. Please confirm the selected HMO is valid and try again.");
            }
        }

        await PopulateProviderFormDropdownsAsync(provider.State, provider.LGA, provider.Level, provider.HmoId);
        return View(provider);
    }

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> Edit(int? id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!await CanManageProviderAsync(provider))
        {
            return Forbid();
        }

        await PopulateProviderFormDropdownsAsync(provider.State, provider.LGA, provider.Level, provider.HmoId);
        return View(provider);
    }

    // EDIT POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> Edit(int id, Provider provider)
    {
        if (id != provider.Id)
        {
            return NotFound();
        }

        var existing = await _context.Providers.FindAsync(id);
        if (existing == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!await CanManageProviderAsync(existing))
        {
            return Forbid();
        }

        ModelState.Remove(nameof(Provider.Code));
        ModelState.Remove(nameof(Provider.DateRegistered));

        NormalizeAndValidateProviderState(provider);
        await ApplyAndValidateProviderHmoAsync(provider, existing.HmoId);

        if (ModelState.IsValid)
        {
            try
            {
                existing.Name = provider.Name;
                existing.Location = provider.Location;
                existing.IsActive = provider.IsActive;
                existing.PatientRatio = provider.PatientRatio;
                existing.Latitude = provider.Latitude;
                existing.Longitude = provider.Longitude;
                existing.State = provider.State;
                existing.LGA = provider.LGA;
                existing.Phone = provider.Phone;
                existing.Email = provider.Email;
                existing.Level = provider.Level;
                existing.HmoId = provider.HmoId;

                await _context.SaveChangesAsync();

                ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync(
                    "Provider.Updated",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    existing.Code,
                    AuditActor.Details(
                        $"Name:{existing.Name}",
                        $"HMO:{existing.HmoId}",
                        $"State:{existing.State}",
                        $"Level:{existing.Level}",
                        $"Active:{existing.IsActive}"),
                    HttpContext.RequestAborted);

                TempData["Success"] = $"Provider {existing.Name} updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Failed to update provider. Please confirm the selected HMO is valid and try again.");
            }
        }

        provider.Code = existing.Code;
        provider.DateRegistered = existing.DateRegistered;
        await PopulateProviderFormDropdownsAsync(provider.State, provider.LGA, provider.Level, provider.HmoId);

        return View(provider);
    }

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var provider = await _context.Providers.FirstOrDefaultAsync(m => m.Id == id);
        if (provider == null) return NotFound();
        if (!await CanManageProviderAsync(provider))
        {
            return Forbid();
        }

        return View(provider);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider == null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await CanManageProviderAsync(provider))
        {
            return Forbid();
        }

        try
        {
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync();
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "Provider.Deleted",
                AuditActor.Format(currentUser, User.Identity?.Name),
                provider.Code,
                AuditActor.Details(
                    $"Name:{provider.Name}",
                    $"HMO:{provider.HmoId}",
                    $"State:{provider.State}",
                    $"Level:{provider.Level}"),
                HttpContext.RequestAborted);
            TempData["Success"] = $"Provider {provider.Name} deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "This provider has linked records. Deactivate it instead of deleting it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (IsHmoOnlyUser())
        {
            return RedirectToAction("MyProviders", "Hmo");
        }

        return RedirectToAction(nameof(Index));
    }

    // Helper: Smart hospital abbreviation
    private string GetHospitalAbbr(string name)
    {
        var words = name.ToUpper().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3 && words.Contains("TEACHING") || words.Contains("UNIVERSITY"))
            return string.Concat(words.Take(4).Select(w => w[0])); // e.g., LUTH, UMTH
        if (words.Contains("GENERAL"))
            return "GH";
        if (words.Contains("SPECIALIST"))
            return "SSH";
        return words.Length > 0 ? new string(words[0].Take(3).ToArray()) : "HOS";
    }

    // DETAILS — GET
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Monitoring,SSHIA,NHIA,IHSA")]
    public async Task<IActionResult> Details(int id)
    {
        var provider = await _context.Providers
            .Include(p => p.Encounters)
            .Include(p => p.Claims)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SSHIA")
            && (string.IsNullOrWhiteSpace(currentUser?.State)
                || !string.Equals(provider.State, currentUser.State.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Forbid();
        }

        // Stats for the view
        ViewBag.TotalEncounters = provider.Encounters?.Count ?? 0;
        ViewBag.TotalClaims = provider.Claims?.Count ?? 0;
        ViewBag.TotalClaimAmount = provider.Claims?.Sum(c => c.Amount) ?? 0;

        return View(provider);
    }

    private bool IsProviderAdmin()
    {
        return User.IsInRole("Admin") || User.IsInRole("CTSHIPAdmin");
    }

    private bool IsHmoOnlyUser()
    {
        return User.IsInRole("HMO") && !IsProviderAdmin();
    }

    private async Task<int?> GetCurrentHmoIdAsync()
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        return currentUser?.HmoId;
    }

    private async Task<bool> CanManageProviderAsync(Provider provider)
    {
        if (IsProviderAdmin())
        {
            return true;
        }

        if (!User.IsInRole("HMO"))
        {
            return false;
        }

        int? hmoId = await GetCurrentHmoIdAsync();
        return hmoId.HasValue && provider.HmoId == hmoId.Value;
    }

    private void NormalizeAndValidateProviderState(Provider provider)
    {
        provider.State = NorthEastLocationData.States
            .FirstOrDefault(state => string.Equals(state, provider.State?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        if (!NorthEastLocationData.IsValidState(provider.State))
        {
            ModelState.AddModelError(nameof(Provider.State), "Select a valid North-East state.");
        }

        provider.LGA = NorthEastLocationData.GetLgas(provider.State)
            .FirstOrDefault(lga => string.Equals(lga, provider.LGA?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        if (!NorthEastLocationData.IsValidLga(provider.State, provider.LGA))
        {
            ModelState.AddModelError(nameof(Provider.LGA), "Select an LGA belonging to the selected state.");
        }
    }

    private async Task ApplyAndValidateProviderHmoAsync(Provider provider, int? existingHmoId = null)
    {
        if (IsHmoOnlyUser())
        {
            ModelState.Remove(nameof(Provider.HmoId));

            int? hmoId = await GetCurrentHmoIdAsync();
            if (!hmoId.HasValue)
            {
                ModelState.AddModelError(nameof(Provider.HmoId), "Your account is not linked to any HMO.");
                return;
            }

            bool hmoExists = await _context.Hmos.AnyAsync(hmo => hmo.Id == hmoId.Value);
            if (!hmoExists)
            {
                ModelState.AddModelError(nameof(Provider.HmoId), "Your linked HMO could not be found.");
                return;
            }

            if (existingHmoId.HasValue && existingHmoId.Value != hmoId.Value)
            {
                ModelState.AddModelError(nameof(Provider.HmoId), "You can only manage providers under your HMO.");
                return;
            }

            provider.HmoId = hmoId.Value;
            return;
        }

        if (provider.HmoId <= 0)
        {
            ModelState.AddModelError(nameof(Provider.HmoId), "Select a valid HMO.");
            return;
        }

        bool selectedHmoExists = await _context.Hmos.AnyAsync(hmo => hmo.Id == provider.HmoId);
        if (!selectedHmoExists)
        {
            ModelState.AddModelError(nameof(Provider.HmoId), "Select a valid HMO.");
        }
    }

    private async Task PopulateProviderFormDropdownsAsync(
        string? selectedState = null,
        string? selectedLga = null,
        string? selectedLevel = null,
        int? selectedHmoId = null)
    {
        var levels = new List<SelectListItem>
    {
        new() { Value = "Tertiary", Text = "Tertiary (Teaching Hospital)" },
        new() { Value = "Secondary", Text = "Secondary (General/Specialist Hospital)" },
        new() { Value = "Private", Text = "Private Hospital/Clinic" },
        new() { Value = "Primary", Text = "Primary Health Centre (PHC)" }
    };

        ViewBag.States = StateSelectListHelper.NorthEastStates(selectedState);
        ViewBag.Lgas = NorthEastLocationData.GetLgas(selectedState)
            .Select(lga => new SelectListItem
            {
                Value = lga,
                Text = lga,
                Selected = string.Equals(lga, selectedLga, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
        ViewBag.Levels = new SelectList(levels, "Value", "Text", selectedLevel);

        bool hmoLocked = IsHmoOnlyUser();
        IQueryable<Hmo> hmos = _context.Hmos.AsNoTracking();

        if (hmoLocked)
        {
            int? currentHmoId = await GetCurrentHmoIdAsync();
            if (currentHmoId.HasValue)
            {
                selectedHmoId = currentHmoId.Value;
                hmos = hmos.Where(hmo => hmo.Id == currentHmoId.Value);
            }
            else
            {
                hmos = hmos.Where(hmo => false);
            }
        }

        ViewBag.HmoLocked = hmoLocked;
        ViewBag.Hmos = await hmos
            .OrderBy(hmo => hmo.Name)
            .Select(hmo => new SelectListItem
            {
                Value = hmo.Id.ToString(),
                Text = hmo.Name,
                Selected = selectedHmoId.HasValue && hmo.Id == selectedHmoId.Value
            })
            .ToListAsync();
    }

    [HttpGet]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public IActionResult GetLgasByState(string state)
    {
        if (!NorthEastLocationData.IsValidState(state))
        {
            return Json(Array.Empty<string>());
        }

        return Json(NorthEastLocationData.GetLgas(state));
    }

    private async Task<string> GenerateProviderCodeAsync(Provider provider)
    {
        // Get state code
        string stateCode = provider.State?.ToUpper() switch
        {
            "ADAMAWA" => "AD",
            "BAUCHI" => "BA",
            "BORNO" => "BO",
            "GOMBE" => "GO",
            "TARABA" => "TA",
            "YOBE" => "YB",
            _ => "NG"
        };

        // Extract hospital abbreviation + append state code if needed
        string abbr = ExtractHospitalAbbreviation(provider.Name, stateCode);

        // Find the highest existing code starting with this abbreviation
        var lastCode = await _context.Providers
            .Where(p => p.Code != null && p.Code.StartsWith(abbr))
            .OrderByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastCode != null && lastCode.Length > abbr.Length)
        {
            if (int.TryParse(lastCode.Substring(abbr.Length), out int num))
                nextNumber = num + 1;
        }

        return $"{abbr}{nextNumber:D3}"; // e.g. UMTH001, FMCAD002
    }




    // Fixed: Now accepts stateCode as parameter
    private string ExtractHospitalAbbreviation(string name, string stateCode)
    {
        if (string.IsNullOrWhiteSpace(name)) return "HSP" + stateCode;

        name = name.ToUpper().Trim();

        var known = new Dictionary<string, string>
    {
        {"UNIVERSITY OF MAIDUGURI TEACHING HOSPITAL", "UMTH"},
        {"ABUBAKAR TAFAWA BALEWA UNIVERSITY TEACHING HOSPITAL", "ATBUTH"},
        {"FEDERAL MEDICAL CENTRE", "FMC"},
        {"FEDERAL TEACHING HOSPITAL", "FTH"},
        {"UNIVERSITY TEACHING HOSPITAL", "UTH"},
        {"GENERAL HOSPITAL", "GH"},
        {"SPECIALIST HOSPITAL", "SH"},
        {"PRIMARY HEALTH CENTRE", "PHC"},
        {"PRIMARY HEALTH CENTER", "PHC"},
        {"CLINIC", "CLN"}
    };

        foreach (var pair in known)
        {
            if (name.Contains(pair.Key))
                return pair.Value + stateCode; // e.g. FMCAD, GHBA
        }

        // Fallback: First letter of each word (max 4) + state code
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                       .Select(w => w.Trim())
                       .Where(w => w.Length > 0)
                       .Select(w => char.IsLetter(w[0]) ? w[0] : 'H')
                       .Take(4);

        return string.Concat(words) + stateCode;
    }


    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> Dashboard()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index", "Home");
        }

        var provider = await _context.Providers
            .Include(p => p.Encounters)
                .ThenInclude(e => e.Enrollee)
            .Include(p => p.Encounters)
                .ThenInclude(e => e.Doctor)
            .Include(p => p.Doctors)
            .Include(p => p.Claims)
            .FirstOrDefaultAsync(p =>
                p.Email == currentUser.Email ||
                p.Phone == currentUser.PhoneNumber ||
                p.Id == currentUser.ProviderId);

        if (provider == null)
        {
            TempData["Error"] = "Your account is not linked to any facility.";
            return RedirectToAction("Index", "Home");
        }

        var enrollees = await _context.Enrollees
            .AsNoTracking()
            .Where(e => e.ProviderId == provider.Id)
            .OrderByDescending(e => e.DateRegistered)
            .ToListAsync();

        // TOP DOCTORS
        var topDoctors = provider.Encounters?
            .Where(e => e.Doctor != null || !string.IsNullOrEmpty(e.SeenBy))
            .GroupBy(e => e.Doctor != null ? e.Doctor.FullName : e.SeenBy!.Trim())
            .Select(g => new TopDoctorStats
            {
                DoctorName = g.Key,
                EncounterCount = g.Count(),
                TotalAmount = g.Sum(e => e.TotalAmount)
            })
            .OrderByDescending(g => g.EncounterCount)
            .Take(5)
            .ToList() ?? new List<TopDoctorStats>();

        var providerServices = await _context.EncounterServices
            .AsNoTracking()
            .Where(x => x.Encounter != null && x.Encounter.ProviderId == provider.Id)
            .GroupBy(x => new { x.ServiceName, x.ServiceSetting })
            .Select(x => new CTSHIPDashboard.Models.ViewModels.ServiceFrequencyViewModel
            {
                ServiceName = x.Key.ServiceName,
                ServiceSetting = x.Key.ServiceSetting,
                Frequency = x.Count()
            })
            .OrderByDescending(x => x.Frequency)
            .ThenBy(x => x.ServiceName)
            .Take(10)
            .ToListAsync();

        string providerIdText = provider.Id.ToString();
        string providerCode = provider.Code ?? string.Empty;
        string providerName = provider.Name ?? string.Empty;
        string providerEmail = provider.Email ?? string.Empty;

        IQueryable<Referral> initiatedReferrals = _context.Referrals
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.FromProviderId == providerIdText ||
                 (!string.IsNullOrWhiteSpace(providerCode) && x.FromProviderId == providerCode) ||
                 x.FromProviderName == providerName));

        IQueryable<Referral> incomingReferrals = _context.Referrals
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.ReferredHospital != null &&
                (x.ReferredHospital.Name == providerName ||
                 (!string.IsNullOrWhiteSpace(providerEmail) &&
                  x.ReferredHospital.Email == providerEmail)));

        int totalReferrals = await initiatedReferrals.CountAsync();
        int completedReferrals = await initiatedReferrals.CountAsync(
            x => x.Status == ReferralStatus.Closed);

        // BUILD VIEWMODEL
        var viewModel = new ProviderDashboardViewModel
        {
            ProviderId = provider.Id,
            ProviderName = provider.Name,
            ProviderCode = provider.Code,
            Level = provider.Level,
            State = provider.State,
            CanUseClaims = ProviderClaimAccessHelper.CanUseClaims(provider),

            TotalUniqueEnrollees = enrollees.Count,
            TotalDoctors = provider.Doctors?.Count(doctor => doctor.IsActive) ?? 0,
            TotalEncounters = provider.Encounters?.Count ?? 0,
            TotalClaims = provider.Claims?.Count ?? 0,
            TotalClaimAmount = provider.Claims?.Sum(c => c.Amount) ?? 0,
            PendingClaims = provider.Claims?.Count(c => c.Status == "Submitted" || c.Status == "Approved") ?? 0,
            PaidClaims = provider.Claims?.Count(c => c.Status == "Paid") ?? 0,
            TotalReferrals = totalReferrals,
            PendingReferralVerification = await initiatedReferrals.CountAsync(
                x => x.Status == ReferralStatus.SubmittedToHmo),
            IncomingReferrals = await incomingReferrals.CountAsync(x =>
                x.Status == ReferralStatus.Verified ||
                x.Status == ReferralStatus.Audited ||
                x.Status == ReferralStatus.Received),
            CompletedReferrals = completedReferrals,
            RejectedReferrals = await initiatedReferrals.CountAsync(
                x => x.Status == ReferralStatus.Rejected),
            ReferralCompletionRate = totalReferrals == 0
                ? 0m
                : Math.Round((decimal)completedReferrals / totalReferrals * 100m, 2),

            RecentEncounters = provider.Encounters?
         .OrderByDescending(e => e.VisitDate)
         .Take(5)
         .ToList() ?? new List<Encounter>(),

            Claims = provider.Claims?.ToList() ?? new List<Claim>(),   // FIXED!
            //ctrl a, alt hoi, alt hoa
            Enrollees = enrollees,
            TopDoctors = topDoctors,
            MostUsedServices = providerServices
        };

        return View(viewModel);
    }

    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> MyEnrollees(string search = "", int page = 1, int pageSize = 20)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.ProviderId.HasValue)
        {
            TempData["Error"] = "Your account is not linked to any healthcare facility.";
            return RedirectToAction("Index", "Home");
        }

        var providerId = currentUser.ProviderId.Value;

        var provider = await _context.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId);

        if (provider == null)
        {
            TempData["Error"] = "Facility not found.";
            return RedirectToAction("Index", "Home");
        }

        var query = _context.Enrollees
            .AsNoTracking()
            .Include(e => e.Hmo)
            .Where(e => e.ProviderId == providerId);

        // SEARCH
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.FullName, s) ||
                EF.Functions.Like(e.EnrollmentNumber, s) ||
                EF.Functions.Like(e.Phone, s));
        }

        var totalItems = await query.CountAsync();

        var enrollees = await query
            .OrderBy(e => e.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // LAST VISIT FOR EACH ENROLLEE
     

        ViewBag.ProviderName = provider.Name;
        ViewBag.ProviderCode = provider.Code;
        ViewBag.ProviderId = provider.Id;
        ViewBag.TotalEnrollees = totalItems;
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(enrollees);
    }

    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> MyEncounters(string search = "", int page = 1, int pageSize = 20)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.ProviderId.HasValue)
        {
            TempData["Error"] = "Your account is not linked to any healthcare facility.";
            return RedirectToAction("Index", "Home");
        }

        var providerId = currentUser.ProviderId.Value;

        var provider = await _context.Providers
            .FirstOrDefaultAsync(p => p.Id == providerId);

        if (provider == null)
        {
            TempData["Error"] = "Facility not found.";
            return RedirectToAction("Index", "Home");
        }

        var query = _context.Encounters
            .OrderByDescending(e => e.VisitDate)
            .Include(e => e.Enrollee)
                .ThenInclude(e => e.Hmo)
            .Include(e => e.Doctor)
            .Where(e => e.ProviderId == providerId);

        // SEARCH
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.EncounterNumber, s) ||
                EF.Functions.Like(e.Enrollee.FullName, s) ||
                EF.Functions.Like(e.Enrollee.EnrollmentNumber, s) ||
                EF.Functions.Like(e.Enrollee.State, s) ||
                EF.Functions.Like(e.ChiefComplaint, s) ||
                EF.Functions.Like(e.Diagnosis, s));
        }

        var totalItems = await query.CountAsync();

        var encounters = await query
            .OrderByDescending(e => e.VisitDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.ProviderName = provider.Name;
        ViewBag.ProviderCode = provider.Code;
        ViewBag.TotalEncounters = totalItems;
        ViewBag.CanUseClaims = ProviderClaimAccessHelper.CanUseClaims(provider);
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(encounters);
    }

    [Authorize(Roles = "Provider,CTSHIPAdmin")]
    public async Task<IActionResult> EncEdit(int id)
    {
        var encounter = await _context.Encounters
            .Include(e => e.Enrollee)
                .ThenInclude(en => en.Hmo)
            .Include(e => e.Provider)
            .Include(e => e.Doctor)
            .Include(e => e.Prescriptions)
            .FirstOrDefaultAsync(e => e.Id == id);
        var currentUser = await _userManager.GetUserAsync(User);

        if (encounter == null)
        {
            TempData["Error"] = "Encounter not found.";
            return RedirectToAction(nameof(Index));
        }

        if (User.IsInRole("Provider") && currentUser?.ProviderId != encounter.ProviderId)
        {
            return Forbid();
        }

        ViewBag.Providers = await _context.Providers
            .Where(p => p.Id == encounter.ProviderId)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} - {p.State}",
                Selected = p.Id == encounter.ProviderId
            })
            .ToListAsync();

        ViewBag.Doctors = await _context.Doctors
            .Where(doctor => doctor.ProviderId == encounter.ProviderId
                && (doctor.IsActive || doctor.Id == encounter.DoctorId))
            .OrderBy(doctor => doctor.FullName)
            .Select(doctor => new SelectListItem
            {
                Value = doctor.Id.ToString(),
                Text = doctor.FullName + " - " + doctor.Specialty,
                Selected = doctor.Id == encounter.DoctorId
            })
            .ToListAsync();

        ViewBag.Statuses = new SelectList(new[]
        {
            "Pending", "Completed", "Cancelled", "Referred", "Claimed"
        }, encounter.Status);
        ViewBag.EncounterReasons = BuildEncounterReasonOptions(encounter.ReasonForEncounter);

        return View(encounter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Provider,CTSHIPAdmin")]
    public async Task<IActionResult> EncEdit(int id, Encounter model)
    {
        if (id != model.Id) return NotFound();

        var encounter = await _context.Encounters
            .Include(item => item.Prescriptions)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (encounter == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (User.IsInRole("Provider") && currentUser?.ProviderId != encounter.ProviderId)
        {
            return Forbid();
        }

        Doctor? doctor = await _context.Doctors.FirstOrDefaultAsync(candidate =>
            candidate.Id == model.DoctorId
            && candidate.ProviderId == encounter.ProviderId
            && (candidate.IsActive || candidate.Id == encounter.DoctorId));

        if (doctor == null)
        {
            ModelState.AddModelError(nameof(model.DoctorId), "Select an active hospital staff member registered under this facility.");
        }

        if (string.IsNullOrWhiteSpace(model.ReasonForEncounter)
            || !EncounterReasons.Contains(model.ReasonForEncounter.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ReasonForEncounter), "Select a valid reason for encounter.");
        }

        if (!ModelState.IsValid)
        {
            model.Enrollee = await _context.Enrollees
                .Include(enrollee => enrollee.Hmo)
                .FirstOrDefaultAsync(enrollee => enrollee.Id == encounter.EnrolleeId);
            model.ProviderId = encounter.ProviderId;
            ViewBag.Providers = await _context.Providers
                .Where(provider => provider.Id == encounter.ProviderId)
                .Select(provider => new SelectListItem
                {
                    Value = provider.Id.ToString(),
                    Text = provider.Name + " - " + provider.State,
                    Selected = provider.Id == encounter.ProviderId
                })
                .ToListAsync();
            ViewBag.Doctors = await _context.Doctors
                .Where(candidate => candidate.ProviderId == encounter.ProviderId
                    && (candidate.IsActive || candidate.Id == encounter.DoctorId))
                .OrderBy(candidate => candidate.FullName)
                .Select(candidate => new SelectListItem
                {
                    Value = candidate.Id.ToString(),
                    Text = candidate.FullName + " - " + candidate.Specialty,
                    Selected = candidate.Id == model.DoctorId
                })
                .ToListAsync();
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Completed", "Cancelled", "Referred", "Claimed" }, model.Status);
            ViewBag.EncounterReasons = BuildEncounterReasonOptions(model.ReasonForEncounter);
            return View(model);
        }

        // Update allowed fields
        encounter.VisitDate = model.VisitDate;
        encounter.ChiefComplaint = model.ChiefComplaint;
        encounter.ReasonForEncounter = model.ReasonForEncounter;
        encounter.Diagnosis = model.Diagnosis;
        encounter.TreatmentGiven = model.TreatmentGiven;
        encounter.Notes = model.Notes;
        encounter.Status = model.Status;
        encounter.DoctorId = doctor!.Id;
        encounter.SeenBy = doctor.FullName;
        encounter.Rank = string.IsNullOrWhiteSpace(doctor.Designation)
            ? doctor.Specialty
            : doctor.Designation;
        encounter.AttendedBy = currentUser?.Email ?? "Unknown User";

        if (!await DeductPendingEncounterPrescriptionsAsync(encounter))
        {
            model.Enrollee = await _context.Enrollees
                .Include(enrollee => enrollee.Hmo)
                .FirstOrDefaultAsync(enrollee => enrollee.Id == encounter.EnrolleeId);
            model.ProviderId = encounter.ProviderId;
            ViewBag.Providers = await _context.Providers
                .Where(provider => provider.Id == encounter.ProviderId)
                .Select(provider => new SelectListItem
                {
                    Value = provider.Id.ToString(),
                    Text = provider.Name + " - " + provider.State,
                    Selected = provider.Id == encounter.ProviderId
                })
                .ToListAsync();
            ViewBag.Doctors = await _context.Doctors
                .Where(candidate => candidate.ProviderId == encounter.ProviderId
                    && (candidate.IsActive || candidate.Id == encounter.DoctorId))
                .OrderBy(candidate => candidate.FullName)
                .Select(candidate => new SelectListItem
                {
                    Value = candidate.Id.ToString(),
                    Text = candidate.FullName + " - " + candidate.Specialty,
                    Selected = candidate.Id == model.DoctorId
                })
                .ToListAsync();
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Completed", "Cancelled", "Referred", "Claimed" }, model.Status);
            ViewBag.EncounterReasons = BuildEncounterReasonOptions(model.ReasonForEncounter);
            return View(model);
        }

        // Recalculate total amount if fees were changed (optional)
        //   encounter.TotalAmount =
        //     (model.ConsultationFee ?? 0) +
        //   (model.LabFee ?? 0) +
        // (model.DrugFee ?? 0);

        _context.Update(encounter);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Encounter {encounter.EncounterNumber} updated successfully!";
        return RedirectToAction("ENCDetails", "Providers", new { id });
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

    private async Task<bool> DeductPendingEncounterPrescriptionsAsync(Encounter encounter)
    {
        if (!string.Equals(encounter.Status, "Completed", StringComparison.OrdinalIgnoreCase))
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

        foreach (EncounterPrescription prescription in pendingPrescriptions)
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

        return true;
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

    [HttpGet]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> CreateClaim(int id, CancellationToken cancellationToken = default)
    {
        Encounter? encounter = await FindClaimableEncounterAsync(id, cancellationToken);
        if (encounter == null)
        {
            TempData["Error"] = "Encounter not found or already has a claim.";
            return RedirectToAction("Index", "Encounters");
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId != encounter.ProviderId)
        {
            return Forbid();
        }

        if (!ProviderClaimAccessHelper.CanUseClaims(encounter.Provider))
        {
            TempData["Error"] = ProviderClaimAccessHelper.ClaimsUnavailableMessage;
            return RedirectToAction(nameof(ENCDetails), new { id });
        }

        if (encounter.Status == "Cancelled")
        {
            TempData["Error"] = "Cancelled encounters cannot be claimed.";
            return RedirectToAction(nameof(ENCDetails), new { id });
        }

        if (encounter.Enrollee?.Hmo == null)
        {
            TempData["Error"] = $"Cannot create claim: {encounter.Enrollee?.FullName} has no HMO assigned.";
            return RedirectToAction(nameof(ENCDetails), new { id });
        }

        return View(BuildClaimSubmissionModel(encounter));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> CreateClaim(
        int id,
        ProviderClaimSubmissionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (id != model.EncounterId)
        {
            return NotFound();
        }

        Encounter? encounter = await FindClaimableEncounterAsync(model.EncounterId, cancellationToken);
        if (encounter == null)
        {
            TempData["Error"] = "Encounter not found or already has a claim.";
            return RedirectToAction("Index", "Encounters");
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId != encounter.ProviderId)
        {
            return Forbid();
        }

        if (!ProviderClaimAccessHelper.CanUseClaims(encounter.Provider))
        {
            TempData["Error"] = ProviderClaimAccessHelper.ClaimsUnavailableMessage;
            return RedirectToAction(nameof(ENCDetails), new { id = encounter.Id });
        }

        if (encounter.Status == "Cancelled")
        {
            TempData["Error"] = "Cancelled encounters cannot be claimed.";
            return RedirectToAction(nameof(ENCDetails), new { id = encounter.Id });
        }

        if (encounter.Enrollee?.Hmo == null)
        {
            TempData["Error"] = $"Cannot create claim: {encounter.Enrollee?.FullName} has no HMO assigned.";
            return RedirectToAction(nameof(ENCDetails), new { id = encounter.Id });
        }

        ValidateClaimEvidenceFiles(model);
        if (!ModelState.IsValid)
        {
            ProviderClaimSubmissionViewModel viewModel = BuildClaimSubmissionModel(encounter);
            return View(viewModel);
        }

        string actorName = currentUser?.FullName ?? currentUser?.Email ?? User.Identity?.Name ?? "Provider";
        List<string> savedEvidencePaths = new();
        Claim claim = new()
        {
            ClaimNumber = "CLM-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
            EnrolleeId = encounter.EnrolleeId,
            ProviderId = encounter.ProviderId,
            HmoId = encounter.Enrollee.HmoId,
            Amount = encounter.TotalAmount,
            Diagnosis = encounter.Diagnosis ?? encounter.ChiefComplaint ?? "Clinical encounter",
            Treatment = encounter.TreatmentGiven ?? "Medical consultation and care",
            DateSubmitted = DateTime.Now,
            Status = "Submitted",
            SubmittedBy = actorName
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Claims.Add(claim);
            await _context.SaveChangesAsync(cancellationToken);

            List<ClaimSupportingDocument> evidenceDocuments = await SaveClaimEvidenceFilesAsync(
                model.EvidenceFiles,
                claim,
                currentUser,
                actorName,
                savedEvidencePaths,
                cancellationToken);

            _context.ClaimSupportingDocuments.AddRange(evidenceDocuments);
            encounter.ClaimId = claim.Id;
            encounter.Status = "Claimed";

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            DeleteSavedClaimEvidenceFiles(savedEvidencePaths);
            ModelState.AddModelError(string.Empty, "Claim submission failed. Please try again.");
            return View(BuildClaimSubmissionModel(encounter));
        }

        await _notificationService.NotifyClaimSubmittedAsync(claim.Id);
        await _auditService.LogAsync(
            "Claim.Submitted",
            AuditActor.Format(currentUser, User.Identity?.Name),
            claim.ClaimNumber,
            AuditActor.Details(
                $"Encounter:{encounter.EncounterNumber}",
                $"Provider:{encounter.Provider?.Name}",
                $"Enrollee:{encounter.Enrollee?.EnrollmentNumber}",
                $"Amount:NGN {claim.Amount:N2}",
                $"EvidenceFiles:{model.EvidenceFiles.Count}"),
            HttpContext.RequestAborted);

        TempData["Success"] = $"Claim {claim.ClaimNumber} successfully submitted with {model.EvidenceFiles.Count} evidence file(s) for {encounter.Enrollee.Hmo.Name}.";
        return RedirectToAction("MyEncounters", "Providers");
    }

    private async Task<Encounter?> FindClaimableEncounterAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Encounters
            .Include(e => e.Enrollee!)
                .ThenInclude(e => e.Hmo!)
            .Include(e => e.Provider!)
            .FirstOrDefaultAsync(e => e.Id == id && e.ClaimId == null, cancellationToken);
    }

    private static ProviderClaimSubmissionViewModel BuildClaimSubmissionModel(Encounter encounter)
    {
        return new ProviderClaimSubmissionViewModel
        {
            EncounterId = encounter.Id,
            EncounterNumber = encounter.EncounterNumber ?? string.Empty,
            VisitDate = encounter.VisitDate,
            EnrolleeName = encounter.Enrollee?.FullName ?? "N/A",
            EnrollmentNumber = encounter.Enrollee?.EnrollmentNumber ?? "N/A",
            HmoName = encounter.Enrollee?.Hmo?.Name ?? "N/A",
            ProviderName = encounter.Provider?.Name ?? "N/A",
            ProviderLevel = encounter.Provider?.Level ?? "N/A",
            Amount = encounter.TotalAmount,
            Diagnosis = encounter.Diagnosis ?? encounter.ChiefComplaint ?? "Clinical encounter",
            Treatment = encounter.TreatmentGiven ?? "Medical consultation and care"
        };
    }

    private void ValidateClaimEvidenceFiles(ProviderClaimSubmissionViewModel model)
    {
        model.EvidenceFiles = (model.EvidenceFiles ?? new List<IFormFile>())
            .Where(file => file is { Length: > 0 })
            .ToList();

        if (model.EvidenceFiles.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.EvidenceFiles),
                "Upload at least one claim evidence file before submitting the claim.");
            return;
        }

        if (model.EvidenceFiles.Count > MaxClaimEvidenceFileCount)
        {
            ModelState.AddModelError(
                nameof(model.EvidenceFiles),
                $"Upload {MaxClaimEvidenceFileCount} or fewer evidence files.");
        }

        foreach (IFormFile file in model.EvidenceFiles)
        {
            ValidateClaimEvidenceFile(file, nameof(model.EvidenceFiles));
        }
    }

    private void ValidateClaimEvidenceFile(IFormFile file, string modelStateKey)
    {
        string extension = Path.GetExtension(file.FileName);
        if (!AllowedClaimEvidenceFileExtensions.Contains(extension))
        {
            ModelState.AddModelError(
                modelStateKey,
                $"{file.FileName} must be a PDF, JPG, PNG, DOC, or DOCX file.");
        }

        if (file.Length > MaxClaimEvidenceFileBytes)
        {
            ModelState.AddModelError(
                modelStateKey,
                $"{file.FileName} must be 10MB or smaller.");
        }
    }

    private async Task<List<ClaimSupportingDocument>> SaveClaimEvidenceFilesAsync(
        IEnumerable<IFormFile> evidenceFiles,
        Claim claim,
        ApplicationUser? currentUser,
        string actorName,
        List<string> savedPhysicalPaths,
        CancellationToken cancellationToken)
    {
        string webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _environment.WebRootPath;
        string claimNumber = SanitizeClaimPathSegment(claim.ClaimNumber);
        string uploadFolder = Path.Combine(webRootPath, "uploads", "claim-support", claimNumber);

        Directory.CreateDirectory(uploadFolder);

        List<ClaimSupportingDocument> documents = new();
        foreach (IFormFile file in evidenceFiles)
        {
            documents.Add(await SaveClaimEvidenceFileAsync(
                file,
                claim.Id,
                claimNumber,
                uploadFolder,
                currentUser,
                actorName,
                savedPhysicalPaths,
                cancellationToken));
        }

        return documents;
    }

    private static async Task<ClaimSupportingDocument> SaveClaimEvidenceFileAsync(
        IFormFile file,
        int claimId,
        string claimNumber,
        string uploadFolder,
        ApplicationUser? currentUser,
        string actorName,
        List<string> savedPhysicalPaths,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string storedFileName = $"claim-evidence-{Guid.NewGuid():N}{extension}";
        string physicalPath = Path.Combine(uploadFolder, storedFileName);

        await using (FileStream stream = new(physicalPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        savedPhysicalPaths.Add(physicalPath);

        return new ClaimSupportingDocument
        {
            ClaimId = claimId,
            DocumentType = "Claim Evidence",
            OriginalFileName = TrimForClaimStorage(Path.GetFileName(file.FileName), 255),
            StoredFileName = storedFileName,
            FilePath = $"/uploads/claim-support/{claimNumber}/{storedFileName}",
            ContentType = TrimForClaimStorage(file.ContentType, 100),
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = currentUser?.Id,
            UploadedByName = actorName
        };
    }

    private static string SanitizeClaimPathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value
            .Where(character => !invalidChars.Contains(character))
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }

    private static string TrimForClaimStorage(string? value, int maxLength)
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

    private static void DeleteSavedClaimEvidenceFiles(IEnumerable<string> physicalPaths)
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
                // Best effort cleanup after a failed claim submission.
            }
        }
    }

    public async Task<IActionResult> ENCDetails(int id)
    {
        var encounter = await _context.Encounters
            .Include(e => e.Enrollee).ThenInclude(e => e!.Hmo)
            .Include(e => e.Provider)
            .Include(e => e.Doctor)
            .Include(e => e.Claim)
            .Include(e => e.Prescriptions)
            .FirstOrDefaultAsync(e => e.Id == id);
        var currentUser = await _userManager.GetUserAsync(User);
        if (encounter == null) return NotFound();
        if (User.IsInRole("Provider") && currentUser?.ProviderId != encounter.ProviderId) return Forbid();
        return View(encounter);
    }

    // DETAILS
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> EnDetails(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .Include(e => e.MedicalHistories)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrollee == null) return NotFound();
        return View(enrollee);
    }

    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> ClaimDetails(int id)
    {
        var claim = await _context.Claims
            .Include(c => c.Enrollee).ThenInclude(e => e!.Hmo)
            .Include(c => c.Provider)
            .Include(c => c.SupportingDocuments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (claim == null) return NotFound();
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.ProviderId != claim.ProviderId) return Forbid();
        if (!ProviderClaimAccessHelper.CanUseClaims(claim.Provider))
        {
            TempData["Error"] = ProviderClaimAccessHelper.ClaimsUnavailableMessage;
            return RedirectToAction(nameof(Dashboard));
        }
        return View(claim);
    }

    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> MyClaims(
    string search = "",
    string status = "All",
    int page = 1,
    int pageSize = 10)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.ProviderId.HasValue)
        {
            TempData["Error"] = "Your account is not linked to any facility.";
            return RedirectToAction("Index", "Home");
        }

        var providerId = currentUser.ProviderId.Value;
        string? providerLevel = await _context.Providers
            .Where(provider => provider.Id == providerId)
            .Select(provider => provider.Level)
            .FirstOrDefaultAsync();

        if (!ProviderClaimAccessHelper.CanUseClaims(providerLevel))
        {
            TempData["Error"] = ProviderClaimAccessHelper.ClaimsUnavailableMessage;
            return RedirectToAction(nameof(Dashboard));
        }

        var query = _context.Claims
            .Include(c => c.Enrollee)
            .Include(c => c.Hmos)
            .Where(c => c.ProviderId == providerId);

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.ClaimNumber, s) ||
                EF.Functions.Like(c.Enrollee.FullName, s) ||
                EF.Functions.Like(c.Enrollee.EnrollmentNumber, s));
        }

        // Status filter
        if (status != "All")
        {
            query = query.Where(c => c.Status == status);
        }

        // Total counts (for dashboard cards)
        ViewBag.TotalSubmitted = await _context.Claims
            .CountAsync(c => c.ProviderId == providerId);

        ViewBag.TotalApproved = await _context.Claims
            .CountAsync(c => c.ProviderId == providerId && c.Status == "Approved");

        ViewBag.TotalPending = await _context.Claims
            .CountAsync(c => c.ProviderId == providerId && (c.Status == "Submitted" || c.Status == "Under Review"));

        ViewBag.TotalPaid = await _context.Claims
            .CountAsync(c => c.ProviderId == providerId && c.Status == "Paid");

        ViewBag.TotalRejected = await _context.Claims
            .CountAsync(c => c.ProviderId == providerId && c.Status == "Rejected");

        ViewBag.TotalClaimAmount = await _context.Claims
            .Where(c => c.ProviderId == providerId)
            .SumAsync(c => c.Amount);

        // Pagination & final list
        var totalItems = await query.CountAsync();

        var claims = await query
            .OrderByDescending(c => c.DateSubmitted)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(claims);
    }

    // ProvidersController.cs
    [Authorize(Roles = "Provider,CTSHIPAdmin,HMO")]
    public async Task<IActionResult> ExportEnrollees(int providerId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (!currentUser.ProviderId.HasValue) return BadRequest();

        var enrollees = await _context.Enrollees
            .Include(e => e.Hmo)
            .Where(c => c.ProviderId == currentUser.ProviderId.Value)
            .Select(e => new
            {
                FullName = e.FullName,
                EnrollmentNumber = e.EnrollmentNumber,
                HMO = e.Hmo != null ? e.Hmo.Name : "N/A",
                Phone = e.Phone,
                State = e.State,
            })
            .ToListAsync();

        if (!enrollees.Any())
        {
            TempData["Error"] = "No enrollees found for this provider.";
            return RedirectToAction("Enrollees", new { id = providerId });
        }

        var excelBytes = ExcelExportHelper.GenerateExcel(enrollees, "MyEnrollees");

        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Enrollees_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> ExportClaims()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (!currentUser.ProviderId.HasValue) return BadRequest();
        string? providerLevel = await _context.Providers
            .Where(provider => provider.Id == currentUser.ProviderId.Value)
            .Select(provider => provider.Level)
            .FirstOrDefaultAsync();

        if (!ProviderClaimAccessHelper.CanUseClaims(providerLevel))
        {
            TempData["Error"] = ProviderClaimAccessHelper.ClaimsUnavailableMessage;
            return RedirectToAction(nameof(Dashboard));
        }

        var claims = await _context.Claims
            .Include(c => c.Enrollee)
            .Include(c => c.Hmos)
            .Where(c => c.ProviderId == currentUser.ProviderId.Value)
            .Select(c => new
            {
                ClaimNumber = c.ClaimNumber,
                Patient = c.Enrollee.FullName,
                EnrollmentID = c.Enrollee.EnrollmentNumber,
                Amount = c.Amount,
                Status = c.Status,
                HMO = c.Hmos.Name,
                Submitted = c.DateSubmitted
            })
            .ToListAsync();

        var excelBytes = ExcelExportHelper.GenerateExcel(claims, "MyClaims");

        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"MyClaims_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

}


