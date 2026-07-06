using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QRCoder;
using System.Drawing;

public class HmoController : Controller
{
    private const string HmoCrudRoles = "CTSHIPAdmin,Admin";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IDeathRegisterService _deathRegisterService;
    private readonly CTSHIPDashboard.Services.IAuditService _auditService;

    public HmoController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment hostEnvironment, IDeathRegisterService deathRegisterService, CTSHIPDashboard.Services.IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
        _auditService = auditService;
        _deathRegisterService = deathRegisterService;
    }


    //death register service injection
    private async Task ApplyDeathStatusToEnrolleeListAsync(List<EnrolleeListViewModel> enrollees, CancellationToken cancellationToken = default)
    {
        Dictionary<int, EnrolleeDeathStatusViewModel> statusById = await _deathRegisterService.GetDeathStatusMapAsync(
            enrollees.Select(x => x.Id),
            cancellationToken);

        Dictionary<string, EnrolleeDeathStatusViewModel> statusByNumber = await _deathRegisterService.GetDeathStatusMapByEnrolleeNumberAsync(
            enrollees.Select(x => x.EnrollmentNumber),
            cancellationToken);

        foreach (EnrolleeListViewModel enrollee in enrollees)
        {
            if (statusById.TryGetValue(enrollee.Id, out EnrolleeDeathStatusViewModel? statusFromId))
            {
                enrollee.DeathStatus = statusFromId;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(enrollee.EnrollmentNumber)
                && statusByNumber.TryGetValue(enrollee.EnrollmentNumber, out EnrolleeDeathStatusViewModel? statusFromNumber))
            {
                enrollee.DeathStatus = statusFromNumber;
                continue;
            }

            enrollee.DeathStatus = EnrolleeDeathStatusViewModel.Active();
        }
    }


    [Authorize(Roles = "HMO,CTSHIPAdmin")]
    public async Task<IActionResult> DisburseMonthly()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to an HMO.";
            return RedirectToAction("Index", "Home");
        }

        HmoBulkDisbursementViewModel model = await BuildDisbursementViewModelAsync(
            currentUser.HmoId.Value,
            new HmoBulkDisbursementViewModel());

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HMO,CTSHIPAdmin")]
    public async Task<IActionResult> DisburseMonthly(HmoBulkDisbursementViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to an HMO.";
            return RedirectToAction("Index", "Home");
        }

        int hmoId = currentUser.HmoId.Value;
        model = await BuildDisbursementViewModelAsync(hmoId, model);

        HmoDisbursementStatusOptionViewModel? selectedStatus = model.StatusOptions
            .FirstOrDefault(option => string.Equals(option.Value, model.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        string? selectedCategory = model.CategoryOptions
            .FirstOrDefault(category => string.Equals(category, model.Category?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selectedStatus == null)
        {
            ModelState.AddModelError(nameof(model.Status), "Select a valid enrollee status.");
        }
        else
        {
            model.Status = selectedStatus.Value;
        }

        if (selectedCategory == null)
        {
            ModelState.AddModelError(nameof(model.Category), "Select a valid disbursement category.");
        }
        else
        {
            model.Category = selectedCategory;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        IQueryable<Enrollee> eligibleQuery = _context.Enrollees
            .Where(enrollee => enrollee.HmoId == hmoId);

        if (!string.Equals(model.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            eligibleQuery = eligibleQuery.Where(enrollee => enrollee.Status == model.Status);
        }

        var eligibleEnrollees = await eligibleQuery
            .Select(enrollee => new
            {
                enrollee.Id,
                enrollee.ProviderId
            })
            .ToListAsync();
        List<int> enrolleeIds = eligibleEnrollees
            .Select(enrollee => enrollee.Id)
            .ToList();

        if (enrolleeIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Status), $"There are no {model.Status.ToLowerInvariant()} enrollees eligible for this disbursement.");
            return View(model);
        }

        DateTime disbursedAt = DateTime.UtcNow;
        List<EnrolleeWallet> existingWallets = await _context.EnrolleeWallets
            .Where(wallet => enrolleeIds.Contains(wallet.EnrolleeId))
            .ToListAsync();

        Dictionary<int, EnrolleeWallet> walletsByEnrollee = existingWallets
            .GroupBy(wallet => wallet.EnrolleeId)
            .ToDictionary(group => group.Key, group => group.OrderBy(wallet => wallet.Id).First());
        bool isMonthlyAllocation = string.Equals(
            model.Category,
            "Monthly Allocation",
            StringComparison.OrdinalIgnoreCase);

        foreach (int enrolleeId in enrolleeIds)
        {
            if (!walletsByEnrollee.TryGetValue(enrolleeId, out EnrolleeWallet? wallet))
            {
                wallet = new EnrolleeWallet
                {
                    EnrolleeId = enrolleeId,
                    Balance = model.AmountPerEnrollee,
                    MonthlyAllocation = isMonthlyAllocation ? model.AmountPerEnrollee : 0m,
                    LastDisbursedAt = disbursedAt
                };
                _context.EnrolleeWallets.Add(wallet);
                walletsByEnrollee[enrolleeId] = wallet;
            }
            else
            {
                wallet.Balance += model.AmountPerEnrollee;
                if (isMonthlyAllocation)
                {
                    wallet.MonthlyAllocation = model.AmountPerEnrollee;
                }

                wallet.LastDisbursedAt = disbursedAt;
            }
        }

        await using var databaseTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            var providerCredits = eligibleEnrollees
                .Where(enrollee => enrollee.ProviderId.HasValue)
                .GroupBy(enrollee => enrollee.ProviderId!.Value)
                .Select(group => new
                {
                    ProviderId = group.Key,
                    Amount = model.AmountPerEnrollee * group.Count()
                })
                .ToList();

            string statusLabel = string.Equals(model.Status, "All", StringComparison.OrdinalIgnoreCase)
                ? "All Statuses"
                : model.Status;

            foreach (var providerCredit in providerCredits)
            {
                await ProviderWalletHelper.CreditAsync(
                    _context,
                    providerCredit.ProviderId,
                    providerCredit.Amount,
                    $"HMO {model.Category} - {statusLabel}",
                    disbursedAt);
            }

            _context.WalletTransactions.AddRange(enrolleeIds.Select(enrolleeId => new WalletTransaction
            {
                EnrolleeWalletId = walletsByEnrollee[enrolleeId].Id,
                Amount = model.AmountPerEnrollee,
                Type = "Disburse",
                Reference = $"HMO {model.Category} - {statusLabel}",
                Timestamp = disbursedAt
            }));

            await _context.SaveChangesAsync();
            await databaseTransaction.CommitAsync();
        }
        catch
        {
            await databaseTransaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "The bulk disbursement could not be completed. No funds were disbursed.");
            model = await BuildDisbursementViewModelAsync(hmoId, model);
            return View(model);
        }

        try
        {
            var actor = User.Identity?.Name ?? "Unknown";
            decimal totalAmount = model.AmountPerEnrollee * enrolleeIds.Count;
            await _auditService.LogAsync(
                "HmoBulkDisbursement",
                actor,
                currentUser.Email,
                $"HmoId:{hmoId}; Category:{model.Category}; Status:{model.Status}; AmountPerEnrollee:{model.AmountPerEnrollee:C}; Total:{totalAmount:C}; Count:{enrolleeIds.Count}");
        }
        catch { }

        string fundedStatus = string.Equals(model.Status, "All", StringComparison.OrdinalIgnoreCase)
            ? "all statuses"
            : model.Status;
        TempData["Success"] = $"{model.Category}: successfully disbursed ₦{model.AmountPerEnrollee:N2} each to {enrolleeIds.Count:N0} {fundedStatus} enrollee(s).";
        return RedirectToAction(nameof(DisburseMonthly));
    }

    private async Task<HmoBulkDisbursementViewModel> BuildDisbursementViewModelAsync(
        int hmoId,
        HmoBulkDisbursementViewModel model)
    {
        var statusGroups = await _context.Enrollees
            .Where(enrollee => enrollee.HmoId == hmoId)
            .GroupBy(enrollee => enrollee.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync();

        List<HmoDisbursementStatusOptionViewModel> statusOptions = statusGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.Status))
            .OrderBy(group => string.Equals(group.Status, "Active", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(group => group.Status)
            .Select(group => new HmoDisbursementStatusOptionViewModel
            {
                Value = group.Status,
                Label = group.Status,
                EligibleCount = group.Count
            })
            .ToList();

        statusOptions.Insert(0, new HmoDisbursementStatusOptionViewModel
        {
            Value = "All",
            Label = "All Statuses",
            EligibleCount = statusGroups.Sum(group => group.Count)
        });

        model.HmoName = await _context.Hmos
            .Where(hmo => hmo.Id == hmoId)
            .Select(hmo => hmo.Name)
            .FirstOrDefaultAsync() ?? "Your HMO";
        model.CategoryOptions = new List<string>
        {
            "Monthly Allocation",
            "Quarterly Allocation",
            "Supplementary Allocation",
            "Special Intervention Fund"
        };
        model.StatusOptions = statusOptions;

        if (string.IsNullOrWhiteSpace(model.Category))
        {
            model.Category = "Monthly Allocation";
        }

        if (string.IsNullOrWhiteSpace(model.Status))
        {
            model.Status = statusOptions.Any(option =>
                string.Equals(option.Value, "Active", StringComparison.OrdinalIgnoreCase))
                ? "Active"
                : "All";
        }

        return model;
    }

    // LIST ALL HMOs
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Index(string search = "")
    {
        var hmos = _context.Hmos.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            hmos = hmos.Where(h => h.Name.ToLower().Contains(search) ||
                                   h.RegistrationNumber.Contains(search) ||
                                   h.Email.ToLower().Contains(search));
        }

        ViewBag.Search = search;
        return View(await hmos.OrderBy(h => h.Name).ToListAsync());
    }

    // CREATE HMO
    [Authorize(Roles = HmoCrudRoles)]
    public IActionResult Create()
    {
        var hmo = new Hmo();
        ViewBag.States = GetHmoStateSelectList(hmo.SelectedStates);
        return View(hmo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Create(Hmo hmo, IFormFile? logo)
    {
        List<string> submittedStates = hmo.SelectedStates.ToList();
        hmo.State = NormalizeNorthEastStates(submittedStates);
        hmo.SelectedStates = ParseNorthEastStates(hmo.State);
        hmo.RegistrationNumber = GenerateHmoRegistrationNumber();
        hmo.DateRegistered = DateTime.UtcNow;
        hmo.Status = "Active";

        ModelState.Remove(nameof(Hmo.State));
        ModelState.Remove(nameof(Hmo.LogoPath));
        ModelState.Remove(nameof(Hmo.RegistrationNumber));
        ModelState.Remove(nameof(Hmo.DateRegistered));
        ModelState.Remove(nameof(Hmo.Status));

        if (hmo.SelectedStates.Count == 0)
        {
            ModelState.AddModelError(nameof(Hmo.SelectedStates), "Select at least one valid North-East state.");
        }
        else if (ContainsInvalidNorthEastState(submittedStates))
        {
            ModelState.AddModelError(nameof(Hmo.SelectedStates), "One or more selected states are not valid North-East states.");
        }

        if (ModelState.IsValid)
        {
            // Upload Logo
            if (logo != null && logo.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/hmos");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{logo.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }
                hmo.LogoPath = "/uploads/hmos/" + fileName;
            }

            _context.Hmos.Add(hmo);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"HMO '{hmo.Name}' registered successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.States = GetHmoStateSelectList(hmo.SelectedStates);
        return View(hmo);
    }

    // EDIT HMO
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Edit(int id)
    {
        var hmo = await _context.Hmos.FindAsync(id);
        if (hmo == null) return NotFound();

        hmo.SelectedStates = ParseNorthEastStates(hmo.State);
        ViewBag.States = GetHmoStateSelectList(hmo.SelectedStates);
        return View(hmo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Edit(int id, Hmo hmo, IFormFile? logo)
    {
        if (id != hmo.Id) return NotFound();

        Hmo? existingHmo = await _context.Hmos.FindAsync(id);
        if (existingHmo == null) return NotFound();

        List<string> submittedStates = hmo.SelectedStates.ToList();
        hmo.State = NormalizeNorthEastStates(submittedStates);
        hmo.SelectedStates = ParseNorthEastStates(hmo.State);
        hmo.Status = NormalizeHmoStatus(hmo.Status);

        ModelState.Remove(nameof(Hmo.State));
        ModelState.Remove(nameof(Hmo.LogoPath));
        ModelState.Remove(nameof(Hmo.RegistrationNumber));
        ModelState.Remove(nameof(Hmo.DateRegistered));

        if (hmo.SelectedStates.Count == 0)
        {
            ModelState.AddModelError(nameof(Hmo.SelectedStates), "Select at least one valid North-East state.");
        }
        else if (ContainsInvalidNorthEastState(submittedStates))
        {
            ModelState.AddModelError(nameof(Hmo.SelectedStates), "One or more selected states are not valid North-East states.");
        }

        if (string.IsNullOrWhiteSpace(hmo.Status))
        {
            ModelState.AddModelError(nameof(hmo.Status), "Select a valid HMO status.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (logo != null && logo.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/hmos");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = $"{Guid.NewGuid()}_{logo.FileName}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await logo.CopyToAsync(stream);
                    }
                    existingHmo.LogoPath = "/uploads/hmos/" + fileName;
                }

                existingHmo.Name = hmo.Name.Trim();
                existingHmo.Email = hmo.Email.Trim();
                existingHmo.Phone = hmo.Phone.Trim();
                existingHmo.Address = hmo.Address.Trim();
                existingHmo.State = hmo.State;
                existingHmo.Status = hmo.Status;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"HMO '{existingHmo.Name}' updated successfully!";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Error updating HMO.";
            }
            return RedirectToAction(nameof(Index));
        }

        ViewBag.States = GetHmoStateSelectList(hmo.SelectedStates);
        return View(hmo);
    }

    // DETAILS
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Details(int id)
    {
        var hmo = await _context.Hmos
            .Include(h => h.Enrollees)
            .Include(h => h.Claims)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hmo == null) return NotFound();

        ViewBag.TotalEnrollees = hmo.Enrollees?.Count ?? 0;
        ViewBag.TotalClaims = hmo.Claims?.Count ?? 0;
        ViewBag.TotalClaimAmount = hmo.Claims?.Sum(c => c.Amount) ?? 0;

        return View(hmo);
    }

    // DELETE GET
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> Delete(int id)
    {
        var hmo = await _context.Hmos
            .Include(h => h.Enrollees)
            .Include(h => h.Claims)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hmo == null) return NotFound();

        ViewBag.CanDelete = (hmo.Enrollees?.Any() != true) && (hmo.Claims?.Any() != true);
        ViewBag.TotalEnrollees = hmo.Enrollees?.Count ?? 0;
        ViewBag.TotalClaims = hmo.Claims?.Count ?? 0;
        return View(hmo);
    }

    // DELETE POST — SAFE DELETE
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = HmoCrudRoles)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var hmo = await _context.Hmos
            .Include(h => h.Enrollees)
            .Include(h => h.Claims)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hmo == null)
        {
            TempData["Error"] = "HMO not found.";
            return RedirectToAction(nameof(Index));
        }

        if (hmo.Enrollees?.Any() == true || hmo.Claims?.Any() == true)
        {
            TempData["Error"] = "Cannot delete HMO with enrolled members or claims. Transfer them first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        _context.Hmos.Remove(hmo);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"HMO {hmo.Name} deleted permanently.";
        return RedirectToAction(nameof(Index));
    }


    [Authorize(Roles = "CTSHIPAdmin,HMO,Monitoring")]
    public async Task<IActionResult> Analytics()
    {
        var hmos = await _context.Hmos
            .Include(h => h.Enrollees)
            .Include(h => h.Claims)
            .ToListAsync();

        var totalEnrollees = hmos.Sum(h => h.Enrollees?.Count ?? 0);

        ViewBag.TotalHmos = hmos.Count;
        ViewBag.TotalEnrollees = totalEnrollees;
        ViewBag.TotalClaims = hmos.Sum(h => h.Claims?.Count ?? 0);
        ViewBag.TotalClaimValue = hmos.Sum(h => h.Claims?.Sum(c => c.Amount) ?? 0);

        // TOP 10 BY ENROLLEES
        ViewBag.TopHmosEnrollees = hmos
            .OrderByDescending(h => h.Enrollees?.Count ?? 0)
            .Take(10)
            .Select((h, i) => new
            {
                Rank = i + 1,
                h.Name,
                h.RegistrationNumber,
                Count = h.Enrollees?.Count ?? 0,
                Percentage = totalEnrollees > 0 ? (double)(h.Enrollees?.Count ?? 0) / totalEnrollees * 100 : 0
            })
            .ToList();

        // TOP 10 BY CLAIMS VALUE
        ViewBag.TopHmosClaims = hmos
            .OrderByDescending(h => h.Claims?.Sum(c => c.Amount) ?? 0)
            .Take(10)
            .Select((h, i) => new
            {
                Rank = i + 1,
                h.Name,
                h.RegistrationNumber,
                ClaimCount = h.Claims?.Count ?? 0,
                TotalAmount = h.Claims?.Sum(c => c.Amount) ?? 0
            })
            .ToList();

        return View();
    }

    [Authorize(Roles ="HMO")]
    public async Task<IActionResult> Dashboard()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index", "Home");
        }

        // METHOD 1: Try to get HMO from user's HmoId (RECOMMENDED)
        Hmo? hmo = null;

        if (currentUser.HmoId.HasValue)
        {
            hmo = await _context.Hmos
                .Include(h => h.Enrollees)
                .Include(h => h.Claims)
                .Include(h => h.Providers)
                .FirstOrDefaultAsync(h => h.Id == currentUser.HmoId.Value);
        }

        // METHOD 2: Fallback — match by email domain or HMO code in username
        if (hmo == null && !string.IsNullOrEmpty(currentUser.Email))
        {
            var emailDomain = currentUser.Email.Split('@').LastOrDefault()?.ToLower();
            var username = currentUser.UserName?.ToLower();

            hmo = await _context.Hmos
                .Include(h => h.Enrollees)
                .Include(h => h.Claims)
                .Include(h => h.Providers)
                .FirstOrDefaultAsync(h =>
                    h.Email.ToLower().Contains(emailDomain!) ||
                    h.RegistrationNumber.ToLower() == username ||
                    h.Name.ToLower().Contains(username ?? ""));
        }

        // FINAL FALLBACK: Show error
        if (hmo == null)
        {
            TempData["Error"] = "Your account is not linked to any HMO. Contact administrator.";
            return RedirectToAction("Index", "Home");
        }

        if (currentUser?.HmoId == null)
        {
            // Handle no HMO
            return View(new List<Provider>());
        }

        // GET ALL PROVIDERS UNDER THE CURRENT USER'S HMO
        var providers = await _context.Providers
            .Where(p => p.HmoId == currentUser.HmoId.Value)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // POPULATE DASHBOARD DATA
        ViewBag.HmoName = hmo.Name;
        ViewBag.HmoCode = hmo.RegistrationNumber;

        ViewBag.EnrolleeCount = hmo.Enrollees?.Count ?? 0;
        ViewBag.ClaimCount = hmo.Claims?.Count ?? 0;
        ViewBag.ProviderCount = hmo.Providers?.Count ?? 0;

        ViewBag.TotalClaimAmount = hmo.Claims?.Sum(c => c.Amount) ?? 0m;
        ViewBag.PendingClaims = hmo.Claims?.Count(c => c.Status == "Submitted") ?? 0;
        ViewBag.PaidClaims = hmo.Claims?.Count(c => c.Status == "Paid") ?? 0;
        ViewBag.ApprovedClaims = hmo.Claims?.Count(c => c.Status == "Approved") ?? 0;
        ViewBag.ComplaintMetrics = await ComplaintMetricsService.BuildAsync(
            _context.Complaints.Where(complaint => complaint.HmoId == hmo.Id));

        // Death registers for this HMO
        string hmoCode = hmo.RegistrationNumber ?? string.Empty;
        var deaths = await _context.DeathRegisters.CountAsync(d => !d.IsDeleted && d.HmoCode == hmoCode && d.Status == DeathRegisterStatus.Audited);
        ViewBag.DeathCount = deaths;
        ViewBag.DeathRatePerThousand = (ViewBag.EnrolleeCount > 0) ? Math.Round((double)deaths / (double)ViewBag.EnrolleeCount * 1000.0, 2) : 0;

        // Wallet statistics for this HMO (sum/avg of wallets for enrollees belonging to this HMO)
        var enrolleeIds = hmo.Enrollees?.Select(e => e.Id).ToList() ?? new List<int>();
        if (enrolleeIds.Any())
        {
            var walletQuery = _context.EnrolleeWallets.Where(w => enrolleeIds.Contains(w.EnrolleeId));
            ViewBag.TotalWalletBalance = await walletQuery.SumAsync(w => (decimal?)w.Balance) ?? 0m;
            ViewBag.AverageWalletBalance = await walletQuery.AverageAsync(w => (decimal?)w.Balance) ?? 0m;
        }
        else
        {
            ViewBag.TotalWalletBalance = 0m;
            ViewBag.AverageWalletBalance = 0m;
        }

        ViewBag.Providers = hmo.Providers ?? new List<Provider>();

        return View(hmo);
    }

    // Helper: Nigerian States
    private List<SelectListItem> GetNigerianStates(string? selectedState = null)
    {
        return StateSelectListHelper.NorthEastStates(selectedState);
    }

    private List<SelectListItem> GetHmoStateSelectList(IEnumerable<string>? selectedStates = null)
    {
        var selected = new HashSet<string>(
            selectedStates ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        return NorthEastLocationData.States
            .Select(state => new SelectListItem
            {
                Value = state,
                Text = state,
                Selected = selected.Contains(state)
            })
            .ToList();
    }

    private static string GenerateHmoRegistrationNumber()
    {
        return $"HMO-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }

    private static string NormalizeNorthEastStates(IEnumerable<string>? states)
    {
        if (states == null)
        {
            return string.Empty;
        }

        List<string> normalizedStates = states
            .Select(NormalizeNorthEastState)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(", ", normalizedStates);
    }

    private static List<string> ParseNorthEastStates(string? states)
    {
        if (string.IsNullOrWhiteSpace(states))
        {
            return new List<string>();
        }

        return states
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeNorthEastState)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsInvalidNorthEastState(IEnumerable<string>? states)
    {
        return states?
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Any(state => !NorthEastLocationData.IsValidState(state)) == true;
    }

    private static string NormalizeNorthEastState(string? state)
    {
        return NorthEastLocationData.States
            .FirstOrDefault(candidate => string.Equals(candidate, state?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static string NormalizeHmoStatus(string? status)
    {
        string normalizedStatus = status?.Trim() ?? string.Empty;

        if (string.Equals(normalizedStatus, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, "true,false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, "on", StringComparison.OrdinalIgnoreCase))
        {
            return "Active";
        }

        if (string.Equals(normalizedStatus, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, "false,false", StringComparison.OrdinalIgnoreCase))
        {
            return "Suspended";
        }

        string[] allowedStatuses = { "Active", "Inactive", "Suspended", "Revoked" };
        return allowedStatuses.FirstOrDefault(candidate =>
            string.Equals(candidate, normalizedStatus, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }


    private bool IsProviderManagementAdmin()
    {
        return User.IsInRole("Admin") || User.IsInRole("CTSHIPAdmin");
    }

    private bool IsHmoOnlyProviderManager()
    {
        return User.IsInRole("HMO") && !IsProviderManagementAdmin();
    }

    private async Task<int?> GetCurrentHmoIdAsync()
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        return currentUser?.HmoId;
    }

    private async Task<bool> CanManageProviderAsync(Provider provider)
    {
        if (IsProviderManagementAdmin())
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

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public IActionResult AddProvider()
    {
        return RedirectToAction("Create", "Providers");
    }

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> ProDetails(int id)
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

        if (!await CanManageProviderAsync(provider))
        {
            return Forbid();
        }

        // Stats for the view
        ViewBag.TotalEncounters = provider.Encounters?.Count ?? 0;
        ViewBag.TotalClaims = provider.Claims?.Count ?? 0;

        return View(provider);
    }

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public IActionResult EditPro(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        return RedirectToAction("Edit", "Providers", new { id = id.Value });
    }

    // EDIT POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public IActionResult EditPro(int id, Provider provider)
    {
        if (id != provider.Id)
        {
            return NotFound();
        }

        TempData["Error"] = "Please review and save this provider from the updated provider form.";
        return RedirectToAction("Edit", "Providers", new { id });
    }

    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public IActionResult ProDelete(int? id)
    {
        if (id == null) return NotFound();
        return RedirectToAction("Delete", "Providers", new { id = id.Value });
    }

    [HttpPost, ActionName("ProDelete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,Admin,HMO")]
    public async Task<IActionResult> ProDeleteConfirmed(int id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction(nameof(MyProviders));
        }

        if (!await CanManageProviderAsync(provider))
        {
            return Forbid();
        }

        try
        {
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Provider {provider.Name} deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "This provider has linked records. Deactivate it instead of deleting it.";
            return RedirectToAction("Details", "Providers", new { id });
        }

        if (IsHmoOnlyProviderManager())
        {
            return RedirectToAction(nameof(MyProviders));
        }

        return RedirectToAction("Index", "Providers");
    }

    [Authorize(Roles = "HMO")]
    public async Task<IActionResult> MyProviders(string search = "", int page = 1, int pageSize = 20)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.HmoId.HasValue)
        {
            TempData["Error"] = "Your account is not linked to any HMO.";
            return RedirectToAction("Index", "Home");
        }

        var hmoId = currentUser.HmoId.Value;

        var hmo = await _context.Hmos
            .Include(h => h.Providers)
            .FirstOrDefaultAsync(h => h.Id == hmoId);

        if (hmo == null)
        {
            TempData["Error"] = "HMO not found.";
            return RedirectToAction("Index", "Home");
        }

        var query = _context.Providers
            .Where(p => p.HmoId == hmoId);

        // SEARCH
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Name, s) ||
                EF.Functions.Like(p.Code, s) ||
                EF.Functions.Like(p.State, s));
        }

        var totalItems = await query.CountAsync();

        var providers = await query
            .OrderBy(p => p.DateRegistered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.HmoName = hmo.Name;
        ViewBag.HmoCode = hmo.RegistrationNumber;
        ViewBag.TotalProviders = totalItems;
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(providers);
    }

    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public async Task<IActionResult> EncountersPerProvider(
    int id,
    string search = "",
    string status = "All",
    int page = 1,
    int pageSize = 20)
    {
        var provider = await _context.Providers
            .Include(p => p.Encounters)
                .ThenInclude(e => e.Enrollee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null)
        {
            TempData["Error"] = "Provider not found.";
            return RedirectToAction("Index");
        }

        var query = _context.Encounters
            .Include(e => e.Enrollee)
                .ThenInclude(e => e.Hmo)
            .Where(e => e.ProviderId == id);

        // SEARCH
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.Enrollee.FullName, s) ||
                EF.Functions.Like(e.Enrollee.EnrollmentNumber, s) ||
                EF.Functions.Like(e.Diagnosis, s) ||
                EF.Functions.Like(e.ChiefComplaint, s));
        }

        // FILTER BY STATUS (if you have encounter status)
        if (status != "All")
        {
            query = query.Where(e => e.Status == status);
        }

        var totalItems = await query.CountAsync();

        var encounters = await query
            .OrderByDescending(e => e.VisitDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.ProviderName = provider.Name;
        ViewBag.ProviderCode = provider.Code;
        ViewBag.ProviderId = id;
        ViewBag.TotalEncounters = totalItems;
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(encounters);
    }

    // DETAILS
    public async Task<IActionResult> EncDetails(int id)
    {
        var encounter = await _context.Encounters
            .Include(e => e.Enrollee).ThenInclude(e => e!.Hmo)
            .Include(e => e.Provider)
            .Include(e => e.Claim)
            .FirstOrDefaultAsync(e => e.Id == id);
        var currentUser = await _userManager.GetUserAsync(User);
        encounter.AttendedBy = currentUser?.Email ?? "Unknown User";
        if (encounter == null) return NotFound();
        return View(encounter);
    }

    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public async Task<IActionResult> AddEnrollee()
    {
        ViewBag.Hmos = await _context.Hmos.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();
        ViewBag.Provider = await _context.Providers.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();

        ViewBag.States = GetNigerianStates();
        ViewBag.LGAs = new List<SelectListItem>(); // Will be populated via AJAX

        return View(new Enrollee());
    }

    // POST: Enrollee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEnrollee(Enrollee enrollee, IFormFile? photo)
    {
        // Remove EnrollmentNumber from validation (we generate it)
        ModelState.Remove("EnrollmentNumber");

        if (!NorthEastLocationData.IsValidState(enrollee.State))
        {
            ModelState.AddModelError(nameof(enrollee.State), "Select a valid North-East state.");
        }
        else if (!NorthEastLocationData.IsValidLga(enrollee.State, enrollee.LGA))
        {
            ModelState.AddModelError(nameof(enrollee.LGA), "Select an LGA belonging to the selected state.");
        }

        if (ModelState.IsValid)
        {
            // 1. Upload Photo (if provided)
            if (enrollee.PhotoFile != null)
            {
                // Delete old photo
                if (!string.IsNullOrEmpty(enrollee.PhotoPath))
                {
                    var oldPath = Path.Combine(_hostEnvironment.WebRootPath, enrollee.PhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads/enrollees");
                var uniqueFileName = $"{enrollee.EnrollmentNumber}_{enrollee.PhotoFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await enrollee.PhotoFile.CopyToAsync(stream);
                }

                enrollee.PhotoPath = "/uploads/enrollees/" + uniqueFileName;
            }

            // 2. AUTO-GENERATE ENROLLMENT NUMBER (Nigerian Standard)
            var lastEnrollee = await _context.Enrollees
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            int nextId = (lastEnrollee?.Id ?? 0) + 1;
            var stateCode = enrollee.State switch
            {
                "Adamawa" => "AD",
                "Borno" => "BN",
                "Bauchi" => "BC",
                "Taraba" => "TR",
                "Yobe" => "YB",
                "Gombe" => "GB",
                _ => "NG"
            };

            //string stateCode = enrollee.State ?? "";
            string year = DateTime.Now.ToString("yyyy");

            enrollee.EnrollmentNumber = $"CTH-{year}-{stateCode}-{nextId:D6}";
            var currentUser = await _userManager.GetUserAsync(User);

            // 3. Set other fields
            enrollee.DateRegistered = DateTime.Now;
            enrollee.Status = "Active";
            enrollee.RegisteredBy = currentUser.Email;

            //check if both name and nin exists
            bool alreadyExists = await _context.Enrollees
                .AnyAsync(e => e.NIN == enrollee.NIN);
            if (alreadyExists)
            {
                ModelState.AddModelError("NIN", "An enrollee with this NIN already exists.");
                ViewBag.Hmos = await _context.Hmos.Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = h.Name
                }).ToListAsync();
                ViewBag.Provider = await _context.Providers.Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = h.Name
                }).ToListAsync();

                ViewBag.States = GetNigerianStates();
                return View(enrollee);
            }

            // 4. Save to database
            _context.Enrollees.Add(enrollee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Enrollee registered successfully! Enrollment ID: {enrollee.EnrollmentNumber}";
            return RedirectToAction(nameof(EnrolleeDashboard));
        }

        // If failed, repopulate dropdowns
        ViewBag.Hmos = await _context.Hmos.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();
        ViewBag.Provider = await _context.Providers.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();

        ViewBag.States = GetNigerianStates();
        //ViewBag.LGAs = GetLGAsByState(enrollee.State);

        return View(enrollee);
    }

    // EDIT
    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public async Task<IActionResult> EditEnrollee(int id)
    {
        var enrollee = await _context.Enrollees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollee == null) return NotFound();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (User.IsInRole("HMO")
            && (!(currentUser?.HmoId.HasValue ?? false) || enrollee.HmoId != currentUser!.HmoId))
        {
            return Forbid();
        }

        await PopulateEnrolleeEditDropdownsAsync(
            enrollee,
            User.IsInRole("HMO") ? currentUser?.HmoId : null);
        return View(enrollee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public async Task<IActionResult> EditEnrollee(int id, Enrollee enrollee)
    {
        if (id != enrollee.Id) return NotFound();
        ModelState.Remove(nameof(Enrollee.EnrollmentNumber));

        Enrollee? existing = await _context.Enrollees.FirstOrDefaultAsync(e => e.Id == id);
        if (existing == null) return NotFound();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? restrictedHmoId = User.IsInRole("HMO") ? currentUser?.HmoId : null;
        if (User.IsInRole("HMO")
            && (!restrictedHmoId.HasValue || existing.HmoId != restrictedHmoId))
        {
            return Forbid();
        }

        if (await _context.Enrollees.AnyAsync(e => e.NIN == enrollee.NIN && e.Id != id))
        {
            ModelState.AddModelError(nameof(Enrollee.NIN), "Another enrollee already uses this NIN.");
        }

        if (enrollee.DateOfBirth >= DateTime.Today)
        {
            ModelState.AddModelError(nameof(Enrollee.DateOfBirth), "Date of birth must be earlier than today.");
        }

        string[] allowedStatuses = ["Active", "Suspended", "Terminated"];
        if (!allowedStatuses.Contains(enrollee.Status, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Enrollee.Status), "Select a valid enrollee status.");
        }

        int? requestedHmoId = existing.HmoId;
        if (enrollee.ProviderId.HasValue
            && !await _context.Providers.AnyAsync(p =>
                p.Id == enrollee.ProviderId.Value
                && (!requestedHmoId.HasValue || p.HmoId == requestedHmoId.Value)))
        {
            ModelState.AddModelError(nameof(Enrollee.ProviderId), "Select a provider assigned to this HMO.");
        }

        if (ModelState.IsValid)
        {
            if (enrollee.PhotoFile != null)
            {
                try
                {
                    existing.PhotoPath = await EnrolleePhotoStorage.SaveAsync(
                        enrollee.PhotoFile,
                        existing.EnrollmentNumber,
                        existing.PhotoPath,
                        _hostEnvironment,
                        HttpContext.RequestAborted);
                }
                catch (InvalidOperationException exception)
                {
                    ModelState.AddModelError(nameof(Enrollee.PhotoFile), exception.Message);
                }
            }

            if (ModelState.IsValid)
            {
                ApplyEnrolleeEditableFields(existing, enrollee);
                existing.HmoId = requestedHmoId;
                existing.ProviderId = enrollee.ProviderId;
                existing.Status = enrollee.Status;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Enrollee updated successfully!";
                if (User.IsInRole("HMO"))
                {
                    return RedirectToAction("EnrolleeDashboard", "Hmo");
                }

                return RedirectToAction("Index", "Enrollees");
            }
        }

        enrollee.EnrollmentNumber = existing.EnrollmentNumber;
        enrollee.DateRegistered = existing.DateRegistered;
        enrollee.PhotoPath = existing.PhotoPath;
        enrollee.HmoId = requestedHmoId;
        await PopulateEnrolleeEditDropdownsAsync(enrollee, restrictedHmoId);
        return View(enrollee);
    }

    private async Task PopulateEnrolleeEditDropdownsAsync(
        Enrollee enrollee,
        int? restrictedHmoId = null)
    {
        ViewBag.States = GetNigerianStates();
        ViewBag.CanChangeHmo = false;

        IQueryable<Hmo> hmoQuery = _context.Hmos.AsNoTracking();
        if (enrollee.HmoId.HasValue)
        {
            hmoQuery = hmoQuery.Where(h => h.Id == enrollee.HmoId.Value);
        }

        ViewBag.Hmos = await hmoQuery
            .OrderBy(h => h.Name)
            .Select(h => new SelectListItem
            {
                Value = h.Id.ToString(),
                Text = h.Name,
                Selected = h.Id == enrollee.HmoId
            })
            .ToListAsync();

        int? providerHmoId = restrictedHmoId ?? enrollee.HmoId;
        IQueryable<Provider> providerQuery = _context.Providers.AsNoTracking();
        if (providerHmoId.HasValue)
        {
            providerQuery = providerQuery.Where(p => p.HmoId == providerHmoId.Value);
        }

        ViewBag.Provider = await providerQuery
            .Where(p => p.IsActive || p.Id == enrollee.ProviderId)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Selected = p.Id == enrollee.ProviderId
            })
            .ToListAsync();
    }

    private static void ApplyEnrolleeEditableFields(Enrollee target, Enrollee source)
    {
        target.FullName = source.FullName.Trim();
        target.Gender = source.Gender;
        target.DateOfBirth = source.DateOfBirth;
        target.Phone = source.Phone.Trim();
        target.NIN = source.NIN;
        target.State = source.State;
        target.LGA = source.LGA.Trim();
        target.Ward = source.Ward.Trim();
        target.Address = source.Address.Trim();
        target.IsPregnant = source.IsPregnant;
        target.HasDisability = source.HasDisability;
        target.IsIdp = source.IsIdp;
        target.OtherVulnerableCategory = source.OtherVulnerableCategory?.Trim();
    }

    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public async Task<IActionResult> EnrolleeDetails(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .Include(e => e.provider)
            .Include(e => e.MedicalHistories)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrollee == null) return NotFound();
        return View(enrollee);
    }

    private async Task<string> GenerateEnrollmentNumber(string state)
    {
        var stateCode = state switch
        {
            "Adamawa" => "AD",
            "Borno" => "BN",
            "Bauchi" => "BC",
            "Taraba" => "TR",
            "Yobe" => "YB",
            "Gombe" => "GB",
            _ => "NG"
        };

        string number;
        do
        {
            var seq = new Random().Next(1, 999999);
            number = $"CTSHIP-{DateTime.Now:yyyy}-{GetStateCode}-{seq:D6}";
        }
        while (await _context.Enrollees.AnyAsync(e => e.EnrollmentNumber == number));

        return number;
    }



    // GET: Enrollee/Card/5
    public async Task<IActionResult> Card(int? id)
    {
        if (id == null) return NotFound();

        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (enrollee == null) return NotFound();

        // Generate QR Code with enrollment number
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(enrollee.EnrollmentNumber + enrollee.FullName, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new BitmapByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        ViewBag.QrCodeImage = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";

        return View(enrollee);
    }
    // Helper: Nigerian States


    // Helper: State Code for Enrollment Number
    private string GetStateCode(string state)
    {
        return state?.ToUpper() switch
        {
            "ADAMAWA" => "AD",
            "BAUCHI" => "BC",
            "BORNO" => "BN",
            "GOMBE" => "GB",
            "TARABA" => "TR",
            "YOBE" => "YB",
            _ => "NG"
        };
    }

    // Optional: Get LGAs by State (you can expand this)
    private List<SelectListItem> GetLGAsByState(string? state)
    {
        // Return dummy or real LGAs based on state
        return new List<SelectListItem>
            {
                new SelectListItem { Value = "Ikeja", Text = "Ikeja" },
                new SelectListItem { Value = "Alimosho", Text = "Alimosho" },
                // Add more per state in real app
            };
    }

    // GET: Enrollee/BulkUpload
    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    public IActionResult BulkUpload()
    {
        return RedirectToAction("BulkUpload", "Enrollees");
    }

    // POST: Enrollee/BulkUpload
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CTSHIPAdmin,HMO")]
    private async Task<IActionResult> LegacyBulkUpload(IFormFile excelFile, int hmoId, int providersId)
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            TempData["Error"] = "Please select an Excel file.";
            return RedirectToAction(nameof(BulkUpload));
        }

        if (!Path.GetExtension(excelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only .xlsx files are allowed.";
            return RedirectToAction(nameof(BulkUpload));
        }

        var enrollees = new List<Enrollee>();
        var errors = new List<string>();
        int rowNumber = 2; // Start from row 2 (after header)

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];

            for (rowNumber = 2; rowNumber <= worksheet.Dimension.End.Row; rowNumber++)
            {
                try
                {
                    var row = worksheet.Cells[rowNumber, 1, rowNumber, 9]; // Adjust columns as needed

                    var fullName = row[rowNumber, 1].GetValue<string>()?.Trim();
                    var gender = row[rowNumber, 2].GetValue<string>()?.Trim();
                    var dobStr = row[rowNumber, 3].GetValue<string>()?.Trim();
                    var phone = row[rowNumber, 4].GetValue<string>()?.Trim();
                    var state = row[rowNumber, 5].GetValue<string>()?.Trim();
                    var lga = row[rowNumber, 6].GetValue<string>()?.Trim();
                    var ward = row[rowNumber, 7].GetValue<string>()?.Trim();
                    var address = row[rowNumber, 8].GetValue<string>()?.Trim();
                    var nin = row[rowNumber, 9].GetValue<long>();


                    if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(state))
                    {
                        errors.Add($"Row {rowNumber}: Missing name or state");
                        continue;
                    }

                    if (!DateTime.TryParse(dobStr, out DateTime dob))
                    {
                        errors.Add($"Row {rowNumber}: Invalid date of birth");
                        continue;
                    }


                    var enrollee = new Enrollee
                    {
                        FullName = fullName!,
                        Gender = gender == "M" || gender == "Male" ? "Male" : "Female",
                        DateOfBirth = dob,
                        Phone = phone ?? "N/A",
                        State = state!,
                        LGA = lga ?? "N/A",
                        Ward = ward ?? "N/A",
                        Address = address ?? "N/A",
                        HmoId = hmoId,
                        ProviderId = providersId,
                        NIN = nin,
                        Status = "Active",
                        DateRegistered = DateTime.Now,
                        RegisteredBy = User.Identity?.Name ?? "Bulk Upload"
                    };

                    // Generate Enrollment Number
                    var stateCode = GetStateCode(state!);
                    var lastEnrollee = await _context.Enrollees
                        .OrderByDescending(e => e.Id)
                        .FirstOrDefaultAsync();

                    int nextSeq = (lastEnrollee?.Id ?? 0) + 1;
                    enrollee.EnrollmentNumber = $"CTH-{DateTime.Now:yyyy}-{stateCode}-{nextSeq:D6}";

                    enrollees.Add(enrollee);
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            if (errors.Any())
            {
                TempData["Error"] = $"Upload completed with errors: {errors.Count} rows failed.";
                TempData["ErrorDetails"] = string.Join("<br>", errors.Take(20));
            }
            else
            {
                _context.Enrollees.AddRange(enrollees);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{enrollees.Count} enrollees uploaded successfully!";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error processing file: " + ex.Message;
        }

        return RedirectToAction(nameof(BulkUpload));
    }

    // DELETE GET — SHOW CONFIRMATION
    public async Task<IActionResult> DeleteEnrollee(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Encounters)
            .Include(e => e.Claims)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollee == null)
        {
            TempData["Error"] = "Enrollee not found.";
            return RedirectToAction(nameof(EnrolleeDashboard));
        }

        // CHECK FOR DEPENDENCIES
        ViewBag.HasEncounters = enrollee.Encounters?.Any() == true;
        ViewBag.HasClaims = enrollee.Claims?.Any() == true;
        ViewBag.CanDelete = !ViewBag.HasEncounters && !ViewBag.HasClaims;

        return View(enrollee);
    }

    // DELETE POST — SAFE & CONFIRMED
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEnrolleeConfirmed(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Encounters)
            .Include(e => e.Claims)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollee == null)
        {
            TempData["Error"] = "Enrollee not found.";
            return RedirectToAction(nameof(EnrolleeDashboard));
        }

        // FINAL SAFETY CHECK — PREVENT ORPHAN RECORDS
        if (enrollee.Encounters?.Any() == true || enrollee.Claims?.Any() == true)
        {
            TempData["Error"] = "Cannot delete enrollee with existing encounters or claims. Delete those first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            // Optional: Delete photo file
            if (!string.IsNullOrEmpty(enrollee.PhotoPath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", enrollee.PhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.Enrollees.Remove(enrollee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Enrollee {enrollee.FullName} ({enrollee.EnrollmentNumber}) deleted permanently.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Failed to delete enrollee. Please try again.";
        }

        return RedirectToAction(nameof(EnrolleeDashboard));
    }

    [Authorize(Roles = "HMO")]
    public async Task<IActionResult> EnrolleeDashboard(
        string search = "",
        string status = "All",
        string state = "All",
        int page = 1,
        int pageSize = 10)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to any HMO.";
            return RedirectToAction("Index", "Home");
        }

        var query = _context.Enrollees.AsQueryable();

        // 1. Primary Filter (HMO ID)
        query = query.Where(e => e.HmoId == currentUser.HmoId.Value);

        // 2. Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e => EF.Functions.Like(e.FullName, s) ||
                                     EF.Functions.Like(e.EnrollmentNumber, s));
        }

        // 3. Status (Ensure exact string match)
        if (status != "All")
        {
            query = query.Where(e => e.Status == status);
        }

        // 4. State (Case-insensitive check for 'all')
        if (!string.IsNullOrWhiteSpace(state) && !state.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.State == state);
        }

        var totalItems = await query.CountAsync(); // Should now return the correct count

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Ensure page is within valid range
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        // PAGINATION - OrderBy is mandatory for stable Skip/Take
        var enrollees = await query
            .OrderByDescending(e => e.DateRegistered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        //var totalRecords = await enrollees.CountAsync();

      
        // VIEW DATA
        ViewBag.HmoName = enrollees.FirstOrDefault()?.Hmo?.Name ?? "Your HMO";
        ViewBag.TotalEnrollees = totalItems;
        ViewBag.ActiveEnrollees = await _context.Enrollees
            .CountAsync(e => e.HmoId == currentUser.HmoId && e.Status == "Active");
        ViewBag.TotalEncounters = await _context.Enrollees
            .Where(e => e.HmoId == currentUser.HmoId)
            .SumAsync(e => e.Encounters.Count);
        ViewBag.TotalClaims = await _context.Enrollees
            .Where(e => e.HmoId == currentUser.HmoId)
            .SumAsync(e => e.Claims.Count);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.State = state;
        ViewBag.CurrentPage = page > 0 ? page : 1;
        ViewBag.TotalPages = totalPages;

        return View(enrollees);
    }

    [Authorize(Roles = "HMO")]
    public async Task<IActionResult> ExportEnrollees()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to any HMO.";
            return RedirectToAction("Dashboard");
        }

        var hmoId = currentUser.HmoId.Value;

        // Get all enrollees under this HMO
        var enrollees = await _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => e.HmoId == hmoId)
            .Select(e => new
            {
                FullName = e.FullName,
                EnrollmentNumber = e.EnrollmentNumber,
                NIN = e.NIN,
                Phone = e.Phone ?? "N/A",
                Gender = e.Gender,
                DateOfBirth = e.DateOfBirth.ToString("dd-MMM-yyyy"),
                State = e.State,
                LGA = e.LGA,
                Ward = e.Ward,
                HMO = e.Hmo.Name,
                Status = (e.Status == "Active") ? "Active" : "Inactive",
                DateRegistered = e.DateRegistered.ToString("dd-MMM-yyyy")
            })
            .OrderBy(e => e.FullName)
            .ToListAsync();

        if (!enrollees.Any())
        {
            TempData["Error"] = "No enrollees found under your HMO.";
            return RedirectToAction("EnrolleeDashboard");
        }

        // Generate Excel
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Enrollees");

        // Header
        ws.Cells[1, 1].LoadFromCollection(enrollees, true);
        var headerRange = ws.Cells[1, 1, 1, ws.Dimension.End.Column];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 100, 0)); // Dark green
        headerRange.Style.Font.Color.SetColor(Color.White);
        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // Auto-fit & date formatting
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        for (int col = 1; col <= ws.Dimension.End.Column; col++)
        {
            if (ws.Cells[2, col].Value is string dateStr && DateTime.TryParse(dateStr, out _))
                ws.Column(col).Style.Numberformat.Format = "dd-MMM-yyyy";
        }

        var excelBytes = package.GetAsByteArray();

        return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"HMO_Enrollees_{currentUser.hmo?.Name ?? "All"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [Authorize(Roles = "HMO")]
    public async Task<IActionResult> ExportClaims()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to any HMO.";
            return RedirectToAction("Dashboard");
        }

        var hmoId = currentUser.HmoId.Value;

        // Get all claims under this HMO
        var claims = (await _context.Claims
           .Include(c => c.Enrollee)
           .Include(c => c.Provider)
           .Where(c => c.HmoId == hmoId)
           .OrderByDescending(c => c.DateSubmitted) // Sort by the raw DateTime first
           .ToListAsync()) // Data is now in-memory
           .Select(c => new
           {
            ClaimNumber = c.ClaimNumber,
            EnrolleeName = c.Enrollee.FullName,
            EnrollmentNumber = c.Enrollee.EnrollmentNumber,
            ProviderName = c.Provider.Name,
            Amount = c.Amount,
            Status = c.Status,
          // Format the date now that we are on the client side
            DateSubmitted = c.DateSubmitted.ToString("dd-MMM-yyyy"),
            Diagnosis = c.Diagnosis ?? "N/A",
            Treatment = c.Treatment ?? "N/A"
           })
           .ToList();


        if (!claims.Any())
        {
            TempData["Error"] = "No claims found under your HMO.";
            return RedirectToAction("MyClaims");
        }

        // Generate Excel
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("HMO Claims");

        // Load data with headers
        ws.Cells[1, 1].LoadFromCollection(claims, true);

        // Style header row
        var headerRange = ws.Cells[1, 1, 1, ws.Dimension.End.Column];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 100, 0)); // Dark green
        headerRange.Style.Font.Color.SetColor(Color.White);
        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // Auto-fit columns & format dates/numbers
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.Column(7).Style.Numberformat.Format = "dd-MMM-yyyy"; // DateSubmitted
        ws.Column(5).Style.Numberformat.Format = "#,##0.00";     // Amount

        var excelBytes = package.GetAsByteArray();

        // Return as downloadable file
        return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"HMO_Claims_{currentUser.hmo?.Name ?? "All"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }
}
