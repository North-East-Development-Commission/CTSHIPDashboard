using AspNetCoreGeneratedDocument;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using CTSHIPDashboard.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

public class ProvidersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<AnalyticsHub> _hubContext;

    public ProvidersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<AnalyticsHub> hubContext)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    // GET: Provider/Index
    [Authorize(Roles = "Admin,HMO,Monitoring,SSHIA")]
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
        ViewBag.TotalProviders = await _context.Providers.CountAsync();
        ViewBag.ActiveProviders = await _context.Providers.CountAsync(p => p.IsActive);
        ViewBag.TotalEnrollees = await _context.Enrollees.CountAsync();
        ViewBag.TotalEncounters = await _context.Encounters.CountAsync();

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
                EnrolleeCount = p.Enrollees.Count,
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

        ViewBag.States = GetNigerianStatesWithAll();
        ViewBag.Levels = new List<SelectListItem>
        {
            new() { Value = "all", Text = "All Levels" },
            new() { Value = "Tertiary", Text = "Tertiary (Teaching Hospitals)" },
            new() { Value = "Secondary", Text = "Secondary (General Hospitals)" },
            new() { Value = "Private", Text = "Private Hospitals" },
            new() { Value = "Primary", Text = "Primary Health Centres" }
        };

        return View(model);
    }

    [Authorize(Roles = "Provider,Admin,HMO")]
    public async Task<IActionResult> WalletSummary(int id)
    {
        var provider = await _context.Providers
            .Include(p => p.Enrollees)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null) return NotFound();

        // For each enrollee under this provider, load wallet
        var enrolleeIds = provider.Enrollees?.Select(e => e.Id).ToList() ?? new List<int>();
        var wallets = await _context.EnrolleeWallets
            .Where(w => enrolleeIds.Contains(w.EnrolleeId))
            .ToListAsync();

        var model = provider;
        ViewBag.Wallets = wallets.ToDictionary(w => w.EnrolleeId, w => w);

        return View(model);
    }

    private List<SelectListItem> GetNigerianStatesWithAll()
    {
        var states = new[] { "Adamawa", "Bauchi","Borno",
            "Gombe",  "Taraba", "Yobe" };

        var list = states.Select(s => new SelectListItem
        {
            Value = s,
            Text = s == "Borno" ? "Borno" : s
        }).OrderBy(s => s.Text).ToList();

        list.Insert(0, new SelectListItem { Value = "all", Text = "All States" });
        return list;
    }


    // GET: Provider/Create
    [Authorize(Roles = "Admin,HMO")]
    public IActionResult Create()
    {
        PopulateDropdowns();
        return View(new Provider());
    }

    // POST: Provider/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Provider provider)
    {
        // We generate Code ourselves → remove from validation
        ModelState.Remove(nameof(Provider.Code));

        if (ModelState.IsValid)
        {
            try
            {
                // Generate smart Nigerian provider code: e.g. UMTH001, FMCYOLA002, GHBAUCHI045
                provider.Code = await GenerateProviderCodeAsync(provider);

                provider.DateRegistered = DateTime.UtcNow;
                provider.IsActive = true;

                _context.Providers.Add(provider);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Provider '{provider.Name}' has been accredited successfully with code: <strong>{provider.Code}</strong>";
                if (User.IsInRole("HMO"))
                {
                    return RedirectToAction("MyProviders", "Hmo");
                }
                else if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Enrollees");
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Unable to save provider. Error: " + ex.Message);
            }
        }

        // If validation fails → repopulate dropdowns with user's choices preserved
        PopulateDropdowns(provider.State, provider.Level);
        return View(provider);
    }

    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> Edit(int? id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction(nameof(Index));
        }

        // Populate dropdowns (if needed in future)
        ViewBag.States = new SelectList(new[]
        {
        "Adamawa", "Bauchi", "Borno",
        "Gombe", "Taraba", "Yobe"
        });

        return View(provider);
    }
    // EDIT POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Provider provider)
    {
        if (id != provider.Id)
        {
            return NotFound();
        }
        //provider.Code = provider.Code;
        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(provider);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Provider {provider.Name} updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Failed to update provider. Please try again.";
            }
        }

        // Repopulate on validation error
        ViewBag.States = new SelectList(new[] {  "Adamawa", "Bauchi", "Borno",
        "Gombe", "Taraba", "Yobe"}, provider?.State);


        return View(provider);
    }

    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var provider = await _context.Providers.FirstOrDefaultAsync(m => m.Id == id);
        if (provider == null) return NotFound();
        return View(provider);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider != null)
        {
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync();
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
    [Authorize(Roles = "Admin,HMO,Monitoring,SSHIA")]
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

        // Stats for the view
        ViewBag.TotalEncounters = provider.Encounters?.Count ?? 0;
        ViewBag.TotalClaims = provider.Claims?.Count ?? 0;
        ViewBag.TotalClaimAmount = provider.Claims?.Sum(c => c.Amount) ?? 0;

        return View(provider);
    }

    private void PopulateDropdowns(string selectedState = null, string selectedLevel = null)
    {
        var states = new List<SelectListItem>
    {
        new() { Value = "Adamawa", Text = "Adamawa" },
        new() { Value = "Bauchi", Text = "Bauchi" },
        new() { Value = "Borno", Text = "Borno" },
        new() { Value = "Gombe", Text = "Gombe" },
        new() { Value = "Taraba", Text = "Taraba" },
        new() { Value = "Yobe", Text = "Yobe" }
    };

        var levels = new List<SelectListItem>
    {
        new() { Value = "", Text = "-- Select Facility Level --" },
        new() { Value = "Tertiary", Text = "Tertiary (Teaching Hospital)" },
        new() { Value = "Secondary", Text = "Secondary (General/Specialist Hospital)" },
        new() { Value = "Private", Text = "Private Hospital/Clinic" },
        new() { Value = "Primary", Text = "Primary Health Centre (PHC)" }
    };

        ViewBag.States = new SelectList(states, "Value", "Text", selectedState);
        ViewBag.Levels = new SelectList(levels, "Value", "Text", selectedLevel);
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

        // UNIQUE ENROLLEES
        var uniqueEnrolleeIds = provider.Encounters?
            .Select(e => e.EnrolleeId)
            .Distinct()
            .ToList() ?? new List<int>();

        var enrollees = await _context.Enrollees
            .Where(e => uniqueEnrolleeIds.Contains(e.Id))
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

        // BUILD VIEWMODEL
        var viewModel = new ProviderDashboardViewModel
        {
            ProviderId = provider.Id,
            ProviderName = provider.Name,
            ProviderCode = provider.Code,
            Level = provider.Level,
            State = provider.State,

            TotalUniqueEnrollees = uniqueEnrolleeIds.Count,
            TotalDoctors = provider.Doctors?.Count(doctor => doctor.IsActive) ?? 0,
            TotalEncounters = provider.Encounters?.Count ?? 0,
            TotalClaims = provider.Claims?.Count ?? 0,
            TotalClaimAmount = provider.Claims?.Sum(c => c.Amount) ?? 0,
            PendingClaims = provider.Claims?.Count(c => c.Status == "Submitted" || c.Status == "Approved") ?? 0,
            PaidClaims = provider.Claims?.Count(c => c.Status == "Paid") ?? 0,

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
            .Include(p => p.Encounters)
            .FirstOrDefaultAsync(p => p.Id == providerId);

        if (provider == null)
        {
            TempData["Error"] = "Facility not found.";
            return RedirectToAction("Index", "Home");
        }

        // GET UNIQUE ENROLLEE IDs FROM ENCOUNTERS AT THIS PROVIDER
        var enrolleeIds = provider.Encounters?
            .Select(e => e.EnrolleeId)
            .Distinct()
            .ToList() ?? new List<int>();

        var query = _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => enrolleeIds.Contains(e.Id));

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
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(encounters);
    }

    [Authorize(Roles = "Provider,Admin")]
    public async Task<IActionResult> EncEdit(int id)
    {
        var encounter = await _context.Encounters
            .Include(e => e.Enrollee)
                .ThenInclude(en => en.Hmo)
            .Include(e => e.Provider)
            .Include(e => e.Doctor)
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
            .Where(p => p.IsActive && (User.IsInRole("Admin") || p.Id == encounter.ProviderId))
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} - {p.State}"
            })
            .ToListAsync();

        ViewBag.Doctors = await _context.Doctors
            .Where(doctor => doctor.ProviderId == encounter.ProviderId
                && (doctor.IsActive || doctor.Id == encounter.DoctorId))
            .OrderBy(doctor => doctor.FullName)
            .Select(doctor => new SelectListItem
            {
                Value = doctor.Id.ToString(),
                Text = doctor.FullName + " — " + doctor.Specialty
            })
            .ToListAsync();

        ViewBag.Statuses = new SelectList(new[]
        {
        "Pending", "Completed", "Cancelled", "Referred", "Claimed"
    });

        return View(encounter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Provider,Admin")]
    public async Task<IActionResult> EncEdit(int id, Encounter model)
    {
        if (id != model.Id) return NotFound();

        var encounter = await _context.Encounters.FindAsync(id);
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
            ModelState.AddModelError(nameof(model.DoctorId), "Select an active doctor registered under this facility.");
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
                    Text = provider.Name + " - " + provider.State
                })
                .ToListAsync();
            ViewBag.Doctors = await _context.Doctors
                .Where(candidate => candidate.ProviderId == encounter.ProviderId
                    && (candidate.IsActive || candidate.Id == encounter.DoctorId))
                .OrderBy(candidate => candidate.FullName)
                .Select(candidate => new SelectListItem
                {
                    Value = candidate.Id.ToString(),
                    Text = candidate.FullName + " — " + candidate.Specialty
                })
                .ToListAsync();
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Completed", "Cancelled", "Referred", "Claimed" });
            return View(model);
        }

        // Update allowed fields
        encounter.VisitDate = model.VisitDate;
        encounter.ChiefComplaint = model.ChiefComplaint;
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
        return RedirectToAction("MyEncounters", "Providers");
    }

    public async Task<IActionResult> ENCDetails(int id)
    {
        var encounter = await _context.Encounters
            .Include(e => e.Enrollee).ThenInclude(e => e!.Hmo)
            .Include(e => e.Provider)
            .Include(e => e.Doctor)
            .Include(e => e.Claim)
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

    public async Task<IActionResult> ClaimDetails(int id)
    {
        var claim = await _context.Claims
            .Include(c => c.Enrollee).ThenInclude(e => e!.Hmo)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (claim == null) return NotFound();
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
    [Authorize(Roles = "Provider,Admin,HMO")]
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

    public async Task<IActionResult> ExportClaims()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (!currentUser.ProviderId.HasValue) return BadRequest();

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
