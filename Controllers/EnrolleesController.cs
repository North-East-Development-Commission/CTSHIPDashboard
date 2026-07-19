using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using CTSHIPDashboard.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using OfficeOpenXml;
using QRCoder;
using System.Drawing;
using System.Globalization;
using System.Diagnostics.Metrics;
using static Bogus.DataSets.Name;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

public class EnrolleesController : Controller
{
    private const string HmoEnrollmentOfficerRole = "HmoEnrollmentOfficer";
    private const string EnrolleeManageRoles = "CTSHIPAdmin,HMO,HmoEnrollmentOfficer";
    private const string EnrolleeViewRoles = "CTSHIPAdmin,HMO,HmoEnrollmentOfficer,Provider,Monitoring";
    private const string EnrolleeDashboardRoles = "HMO,HmoEnrollmentOfficer";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IDeathRegisterService _deathRegisterService;
    private readonly CTSHIPDashboard.Services.IAuditService _auditService;

    public EnrolleesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment hostEnvironment, IDeathRegisterService deathRegisterService, CTSHIPDashboard.Services.IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
        _deathRegisterService = deathRegisterService;
        _auditService = auditService;
    }

    private bool IsHmoEnrollmentScopedUser()
    {
        return User.IsInRole("HMO") || User.IsInRole(HmoEnrollmentOfficerRole);
    }

    private IActionResult RedirectAfterEnrollmentChange()
    {
        if (User.IsInRole(HmoEnrollmentOfficerRole))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        if (User.IsInRole("HMO"))
        {
            return RedirectToAction("EnrolleeDashboard", "Hmo");
        }

        return RedirectToAction(nameof(Index));
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


    // INDEX — ALL ENROLLEES
    // GET: /Enrollee or /Enrollee/Index
    [Authorize(Roles = "CTSHIPAdmin,HMO,HmoEnrollmentOfficer,Monitoring")]
    public async Task<IActionResult> Index(
        string search = "",      // Search by name, phone, NIN, or enrollment number
        string status = "",      // "Active", "Inactive", "Suspended", etc.
        string state = "",       // Optional: filter by state
        string hmo = "",         // Optional: filter by HMO
        int page = 1,
        int pageSize = 15)
    {
        // Start building query
        var enrollees = _context.Enrollees
            .Include(e => e.Hmo)
            .AsQueryable();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? restrictedHmoId = IsHmoEnrollmentScopedUser() ? currentUser?.HmoId : null;
        if (IsHmoEnrollmentScopedUser() && !restrictedHmoId.HasValue)
        {
            TempData["Error"] = "Your account is not linked to an HMO.";
            return RedirectToAction("Index", "Home");
        }

        if (restrictedHmoId.HasValue)
        {
            enrollees = enrollees.Where(e => e.HmoId == restrictedHmoId.Value);
            hmo = restrictedHmoId.Value.ToString();
        }

        // SEARCH — Smart multi-field search
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLowerInvariant();

            enrollees = enrollees.Where(e =>
                e.FullName.ToLower().Contains(search) ||
                e.EnrollmentNumber.Contains(search) ||
                e.Phone.Contains(search) ||
                (e.NIN.ToString().Contains(search))
            );
        }

        // FILTER: Status
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            enrollees = enrollees.Where(e => e.Status == status);
        }

        // FILTER: State
        if (!string.IsNullOrWhiteSpace(state) && state != "all")
        {
            enrollees = enrollees.Where(e => e.State == state);
        }

        // FILTER: HMO
        if (!string.IsNullOrWhiteSpace(hmo) && hmo != "all")
        {
            enrollees = enrollees.Where(e => e.HmoId.ToString() == hmo || (e.Hmo != null && e.Hmo.Name == hmo));
        }

        // SORT: Newest first (or by name if needed)
        enrollees = enrollees.OrderByDescending(e => e.DateRegistered);

        // PAGINATION
        var totalRecords = await enrollees.CountAsync();
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var model = await enrollees
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnrolleeListViewModel
            {
                Id = e.Id,
                FullName = e.FullName,
                EnrollmentNumber = e.EnrollmentNumber,
                Gender = e.Gender,
                Phone = e.Phone,
                State = e.State,
                NIN = e.NIN,
                HmoName = e.Hmo != null ? e.Hmo.Name : "Not Assigned",
                Status = e.Status,
                DateRegistered = e.DateRegistered,
                PhotoPath = e.PhotoPath ?? "/img/icon-192.png"
            })
            .ToListAsync();

        // ViewBag — for filters, pagination & dropdowns
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.State = state;
        ViewBag.Hmo = hmo;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;
        ViewBag.HasPrevious = page > 1;
        ViewBag.HasNext = page < totalPages;
        ViewBag.PageSize = pageSize;

        // Dropdown data
        ViewBag.StatusList = new SelectList(new[]
        {
        new { Value = "", Text = "All Status" },
        new { Value = "Active", Text = "Active" },
        new { Value = "Inactive", Text = "Inactive" },
        new { Value = "Suspended", Text = "Suspended" }
    }, "Value", "Text", status);

        IQueryable<Enrollee> stateQuery = _context.Enrollees.AsNoTracking();
        if (restrictedHmoId.HasValue)
        {
            stateQuery = stateQuery.Where(e => e.HmoId == restrictedHmoId.Value);
        }

        ViewBag.StateList = new SelectList(await stateQuery
            .Select(e => e.State).Distinct().OrderBy(s => s).ToListAsync(), state);

        IQueryable<Hmo> hmoQuery = _context.Hmos.AsNoTracking();
        if (restrictedHmoId.HasValue)
        {
            hmoQuery = hmoQuery.Where(h => h.Id == restrictedHmoId.Value);
        }

        ViewBag.HmoList = new SelectList(await hmoQuery
            .Select(h => new { h.Id, h.Name })
            .OrderBy(h => h.Name)
            .ToListAsync(), "Id", "Name", hmo);

        return View(model);
    }

    // CREATE
    // GET: Enrollee/Create
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> Create()
    {
        var enrollee = new Enrollee();
        await PopulateCreateDropdownsAsync(enrollee);
        return View(enrollee);
    }

    // POST: Enrollee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> Create(Enrollee enrollee)
    {
        // Remove EnrollmentNumber from validation (we generate it)
        ModelState.Remove(nameof(Enrollee.EnrollmentNumber));

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (IsHmoEnrollmentScopedUser())
        {
            if (currentUser?.HmoId == null)
            {
                ModelState.AddModelError(nameof(Enrollee.HmoId), "Your account is not linked to an HMO.");
            }
            else
            {
                enrollee.HmoId = currentUser.HmoId.Value;
            }
        }

        if (!NorthEastLocationData.IsValidState(enrollee.State))
        {
            ModelState.AddModelError(nameof(enrollee.State), "Select a valid North-East state.");
        }
        else if (!NorthEastLocationData.IsValidLga(enrollee.State, enrollee.LGA))
        {
            ModelState.AddModelError(nameof(enrollee.LGA), "Select an LGA belonging to the selected state.");
        }

        if (enrollee.DateOfBirth >= DateTime.Today)
        {
            ModelState.AddModelError(nameof(enrollee.DateOfBirth), "Date of birth must be earlier than today.");
        }

        await ValidateEnrollmentAssignmentAsync(enrollee, "Select a facility assigned to the selected HMO.");

        //check if both name and nin exists
        bool alreadyExists = await _context.Enrollees
            .AnyAsync(e => e.NIN == enrollee.NIN);
        if (alreadyExists)
        {
            ModelState.AddModelError(nameof(Enrollee.NIN), "An enrollee with this NIN already exists.");
        }

        if (ModelState.IsValid)
        {
            enrollee.EnrollmentNumber = await GenerateEnrollmentNumber(enrollee.State);

            if (enrollee.PhotoFile != null)
            {
                try
                {
                    enrollee.PhotoPath = await EnrolleePhotoStorage.SaveAsync(
                        enrollee.PhotoFile,
                        enrollee.EnrollmentNumber,
                        null,
                        _hostEnvironment,
                        HttpContext.RequestAborted);
                }
                catch (InvalidOperationException exception)
                {
                    ModelState.AddModelError(nameof(Enrollee.PhotoFile), exception.Message);
                    await PopulateCreateDropdownsAsync(enrollee);
                    return View(enrollee);
                }
            }

            // 3. Set other fields
            enrollee.DateRegistered = DateTime.Now;
            enrollee.Status = "Active";
            enrollee.RegisteredBy = currentUser?.Email ?? User.Identity?.Name;

            // 4. Save to database
            _context.Enrollees.Add(enrollee);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "Enrollee.Created",
                AuditActor.Format(currentUser, User.Identity?.Name),
                enrollee.EnrollmentNumber,
                AuditActor.Details(
                    $"Name:{enrollee.FullName}",
                    $"HMO:{enrollee.HmoId}",
                    $"Provider:{enrollee.ProviderId}",
                    $"State:{enrollee.State}"),
                HttpContext.RequestAborted);

            TempData["Success"] = $"Enrollee registered successfully! Enrollment ID: {enrollee.EnrollmentNumber}";
            return RedirectAfterEnrollmentChange();
        }

        // If failed, repopulate dropdowns
        await PopulateCreateDropdownsAsync(enrollee);
        return View(enrollee);
    }

    private async Task PopulateCreateDropdownsAsync(Enrollee enrollee)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? restrictedHmoId = IsHmoEnrollmentScopedUser() ? currentUser?.HmoId : null;

        if (restrictedHmoId.HasValue)
        {
            enrollee.HmoId = restrictedHmoId.Value;
        }

        ViewBag.CanChangeHmo = !restrictedHmoId.HasValue;

        IQueryable<Hmo> hmoQuery = _context.Hmos.AsNoTracking();
        if (restrictedHmoId.HasValue)
        {
            hmoQuery = hmoQuery.Where(h => h.Id == restrictedHmoId.Value);
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
        IQueryable<Provider> providerQuery = _context.Providers.AsNoTracking()
            .Where(p => p.IsActive || p.Id == enrollee.ProviderId);

        if (providerHmoId.HasValue)
        {
            providerQuery = providerQuery.Where(p => p.HmoId == providerHmoId.Value);
        }

        ViewBag.Provider = await providerQuery
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Selected = p.Id == enrollee.ProviderId
            })
            .ToListAsync();

        ViewBag.States = GetNigerianStates();
        ViewBag.LGAs = new List<SelectListItem>();
    }

    private async Task ValidateEnrollmentAssignmentAsync(Enrollee enrollee, string providerErrorMessage)
    {
        if (!enrollee.HmoId.HasValue)
        {
            ModelState.AddModelError(nameof(Enrollee.HmoId), "Select an HMO.");
        }
        else if (!await _context.Hmos.AnyAsync(h => h.Id == enrollee.HmoId.Value))
        {
            ModelState.AddModelError(nameof(Enrollee.HmoId), "Select a valid HMO.");
        }

        if (!enrollee.ProviderId.HasValue)
        {
            ModelState.AddModelError(nameof(Enrollee.ProviderId), "Select an assigned facility.");
        }
        else if (!await _context.Providers.AnyAsync(p =>
            p.Id == enrollee.ProviderId.Value
            && p.HmoId == enrollee.HmoId
            && p.IsActive))
        {
            ModelState.AddModelError(nameof(Enrollee.ProviderId), providerErrorMessage);
        }
    }

    // EDIT
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> Edit(int id)
    {
        var enrollee = await _context.Enrollees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollee == null) return NotFound();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (IsHmoEnrollmentScopedUser()
            && (!(currentUser?.HmoId.HasValue ?? false) || enrollee.HmoId != currentUser!.HmoId))
        {
            return Forbid();
        }

        await PopulateEditDropdownsAsync(enrollee, IsHmoEnrollmentScopedUser() ? currentUser?.HmoId : null);
        return View(enrollee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> Edit(int id, Enrollee enrollee)
    {
        if (id != enrollee.Id) return NotFound();
        ModelState.Remove(nameof(Enrollee.EnrollmentNumber));

        Enrollee? existing = await _context.Enrollees.FirstOrDefaultAsync(e => e.Id == id);
        if (existing == null) return NotFound();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? restrictedHmoId = IsHmoEnrollmentScopedUser() ? currentUser?.HmoId : null;
        if (IsHmoEnrollmentScopedUser()
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
            ModelState.AddModelError(nameof(Enrollee.ProviderId), "Select a facility assigned to the selected HMO.");
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
                ApplyEditableFields(existing, enrollee);
                existing.HmoId = requestedHmoId;
                existing.ProviderId = enrollee.ProviderId;
                existing.Status = enrollee.Status;

                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    "Enrollee.Updated",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    existing.EnrollmentNumber,
                    AuditActor.Details(
                        $"Name:{existing.FullName}",
                        $"Status:{existing.Status}",
                        $"HMO:{existing.HmoId}",
                        $"Provider:{existing.ProviderId}",
                        enrollee.PhotoFile != null ? "Photo:Updated" : null),
                    HttpContext.RequestAborted);

                TempData["Success"] = "Enrollee updated successfully!";
                return RedirectAfterEnrollmentChange();
            }
        }

        enrollee.EnrollmentNumber = existing.EnrollmentNumber;
        enrollee.DateRegistered = existing.DateRegistered;
        enrollee.PhotoPath = existing.PhotoPath;
        enrollee.HmoId = requestedHmoId;
        await PopulateEditDropdownsAsync(enrollee, restrictedHmoId);
        return View(enrollee);
    }

    private async Task PopulateEditDropdownsAsync(Enrollee enrollee, int? restrictedHmoId = null)
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

    private static void ApplyEditableFields(Enrollee target, Enrollee source)
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

    // DETAILS
    [Authorize(Roles = EnrolleeViewRoles)]
    public async Task<IActionResult> Details(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .Include(e => e.MedicalHistories)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrollee == null) return NotFound();

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (IsHmoEnrollmentScopedUser()
            && (!(currentUser?.HmoId.HasValue ?? false) || enrollee.HmoId != currentUser!.HmoId))
        {
            return Forbid();
        }

        return View(enrollee);
    }

    // GENERATE UNIQUE ENROLLMENT NUMBER
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
            number = $"CTH-{DateTime.Now:yyyy}-{GetStateCode(state)}-{seq:D6}";
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
    private List<SelectListItem> GetNigerianStates()
    {
        return StateSelectListHelper.NorthEastStates();
    }

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

    [HttpGet]
    [Authorize(Roles = EnrolleeManageRoles)]
    public IActionResult GetLgasByState(string state)
    {
        if (!NorthEastLocationData.IsValidState(state))
        {
            return Json(Array.Empty<string>());
        }

        return Json(NorthEastLocationData.GetLgas(state));
    }

    [HttpGet]
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> GetWardsByLga(string state, string lga)
    {
        if (!NorthEastLocationData.IsValidLga(state, lga))
        {
            return Json(Array.Empty<string>());
        }

        List<string> wards = await _context.Enrollees
            .AsNoTracking()
            .Where(enrollee =>
                enrollee.State == state
                && enrollee.LGA == lga
                && enrollee.Ward != string.Empty)
            .Select(enrollee => enrollee.Ward)
            .Distinct()
            .OrderBy(ward => ward)
            .Take(250)
            .ToListAsync();

        return Json(wards);
    }

    // GET: Enrollee/BulkUpload
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> BulkUpload()
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        IQueryable<Hmo> hmos = _context.Hmos.AsNoTracking();
        IQueryable<Provider> providers = _context.Providers.AsNoTracking();

        if (IsHmoEnrollmentScopedUser())
        {
            if (currentUser?.HmoId == null)
            {
                TempData["Error"] = "Your account is not linked to an HMO.";
                ViewBag.Hmos = new List<SelectListItem>();
                ViewBag.Pros = new List<SelectListItem>();
                return View();
            }

            int currentHmoId = currentUser.HmoId.Value;
            hmos = hmos.Where(hmo => hmo.Id == currentHmoId);
            providers = providers.Where(provider => provider.HmoId == currentHmoId);
        }

        ViewBag.Hmos = await hmos.OrderBy(hmo => hmo.Name).Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();
        ViewBag.Pros = await providers
            .Where(provider => provider.IsActive)
            .OrderBy(provider => provider.Name)
            .Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();

        return View();
    }

    [HttpGet]
    [Authorize(Roles = EnrolleeManageRoles)]
    public IActionResult DownloadBulkUploadTemplate()
    {
        using var package = new ExcelPackage();
        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Enrollee Upload");

        for (int index = 0; index < BulkEnrolleeUploadSchema.Columns.Count; index++)
        {
            BulkEnrolleeColumn column = BulkEnrolleeUploadSchema.Columns[index];
            worksheet.Cells[1, index + 1].Value = column.Header;
            worksheet.Cells[2, index + 1].Value = column.Example;
        }

        worksheet.Cells[2, 3].Value = new DateTime(1992, 4, 18);
        worksheet.Cells[2, 3].Style.Numberformat.Format = "dd/mm/yyyy";
        worksheet.Cells[2, 4].Style.Numberformat.Format = "@";
        worksheet.Cells[2, 5].Style.Numberformat.Format = "@";

        using (ExcelRange header = worksheet.Cells[1, 1, 1, BulkEnrolleeUploadSchema.Columns.Count])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(Color.White);
            header.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 112, 59));
            header.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }

        using (ExcelRange sample = worksheet.Cells[2, 1, 2, BulkEnrolleeUploadSchema.Columns.Count])
        {
            sample.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            sample.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 242, 228));
        }

        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        worksheet.Column(9).Width = Math.Max(worksheet.Column(9).Width, 32);
        worksheet.View.FreezePanes(2, 1);
        worksheet.Cells[1, 1, 2, BulkEnrolleeUploadSchema.Columns.Count].AutoFilter = true;

        ExcelWorksheet guide = package.Workbook.Worksheets.Add("Upload Guide");
        guide.Cells["A1"].Value = "CTSHIP Bulk Enrollee Upload Guide";
        guide.Cells["A1:C1"].Merge = true;
        guide.Cells["A1"].Style.Font.Bold = true;
        guide.Cells["A1"].Style.Font.Size = 16;
        guide.Cells["A1"].Style.Font.Color.SetColor(Color.White);
        guide.Cells["A1"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        guide.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 112, 59));
        guide.Cells["A3"].Value = "Column";
        guide.Cells["B3"].Value = "Requirement";
        guide.Cells["C3"].Value = "Example";

        for (int index = 0; index < BulkEnrolleeUploadSchema.Columns.Count; index++)
        {
            BulkEnrolleeColumn column = BulkEnrolleeUploadSchema.Columns[index];
            int row = index + 4;
            guide.Cells[row, 1].Value = column.Header;
            guide.Cells[row, 2].Value = column.Description;
            guide.Cells[row, 3].Value = column.Example;
        }

        guide.Cells["A14"].Value =
            "Choose the HMO and Provider on the upload page. Do not add them as spreadsheet columns.";
        guide.Cells["A14:C14"].Merge = true;
        guide.Cells["A15"].Value =
            "Row 2 is an example only. Replace it with real enrollee data or delete it before uploading.";
        guide.Cells["A15:C15"].Merge = true;
        guide.Cells["A3:C3"].Style.Font.Bold = true;
        guide.Cells["A3:C3"].Style.Font.Color.SetColor(Color.White);
        guide.Cells["A3:C3"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        guide.Cells["A3:C3"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(254, 144, 49));
        guide.Cells[guide.Dimension.Address].AutoFitColumns();
        guide.Column(2).Width = Math.Max(guide.Column(2).Width, 60);
        guide.Cells.Style.WrapText = true;

        return File(
            package.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CTSHIP-Enrollee-Bulk-Upload-Template.xlsx");
    }

    // POST: Enrollee/BulkUpload
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = EnrolleeManageRoles)]
    public async Task<IActionResult> BulkUpload(IFormFile excelFile, int hmoId, int providerId)
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

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (IsHmoEnrollmentScopedUser())
        {
            if (currentUser?.HmoId == null)
            {
                TempData["Error"] = "Your account is not linked to an HMO.";
                return RedirectToAction(nameof(BulkUpload));
            }

            hmoId = currentUser.HmoId.Value;
        }

        Hmo? selectedHmo = await _context.Hmos
            .AsNoTracking()
            .FirstOrDefaultAsync(hmo => hmo.Id == hmoId);
        Provider? selectedProvider = await _context.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(provider =>
                provider.Id == providerId
                && provider.HmoId == hmoId
                && provider.IsActive);

        if (selectedHmo == null || selectedProvider == null)
        {
            TempData["Error"] = "Select a valid HMO and an active provider assigned to that HMO.";
            return RedirectToAction(nameof(BulkUpload));
        }

        List<Enrollee> enrollees = new();
        List<string> errors = new();
        int rowNumber = 2;

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            ExcelWorksheet? worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null)
            {
                TempData["Error"] = "The workbook does not contain any enrollee data.";
                return RedirectToAction(nameof(BulkUpload));
            }

            Dictionary<string, int> headerMap = new(StringComparer.OrdinalIgnoreCase);
            for (int column = 1; column <= worksheet.Dimension.End.Column; column++)
            {
                string normalized = BulkEnrolleeUploadSchema.NormalizeHeader(
                    worksheet.Cells[1, column].Text);
                if (!string.IsNullOrWhiteSpace(normalized) && !headerMap.ContainsKey(normalized))
                {
                    headerMap[normalized] = column;
                }
            }

            List<string> missingHeaders = BulkEnrolleeUploadSchema.RequiredHeaders
                .Where(header => !headerMap.ContainsKey(
                    BulkEnrolleeUploadSchema.NormalizeHeader(header)))
                .ToList();

            if (missingHeaders.Any())
            {
                TempData["Error"] =
                    "The Excel columns do not match the upload template. Missing required columns: "
                    + string.Join(", ", missingHeaders);
                return RedirectToAction(nameof(BulkUpload));
            }

            int Column(string header) =>
                headerMap[BulkEnrolleeUploadSchema.NormalizeHeader(header)];

            HashSet<long> knownNins = await _context.Enrollees
                .AsNoTracking()
                .Select(enrollee => enrollee.NIN)
                .ToHashSetAsync();
            int nextSequence = await _context.Enrollees
                .AsNoTracking()
                .MaxAsync(enrollee => (int?)enrollee.Id) ?? 0;

            Dictionary<string, string> validStates = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Adamawa"] = "Adamawa",
                ["Bauchi"] = "Bauchi",
                ["Borno"] = "Borno",
                ["Gombe"] = "Gombe",
                ["Taraba"] = "Taraba",
                ["Yobe"] = "Yobe"
            };

            for (rowNumber = 2; rowNumber <= worksheet.Dimension.End.Row; rowNumber++)
            {
                try
                {
                    string fullName = worksheet.Cells[rowNumber, Column("FullName")].Text.Trim();
                    string genderValue = worksheet.Cells[rowNumber, Column("Gender")].Text.Trim();
                    ExcelRange dobCell = worksheet.Cells[rowNumber, Column("DateOfBirth")];
                    string phone = worksheet.Cells[rowNumber, Column("Phone")].Text.Trim();
                    string ninValue = worksheet.Cells[rowNumber, Column("NIN")].Text.Trim();
                    string stateValue = worksheet.Cells[rowNumber, Column("State")].Text.Trim();
                    string lga = worksheet.Cells[rowNumber, Column("LGA")].Text.Trim();
                    string ward = worksheet.Cells[rowNumber, Column("Ward")].Text.Trim();
                    string address = worksheet.Cells[rowNumber, Column("Address")].Text.Trim();

                    if (new[] { fullName, genderValue, phone, ninValue, stateValue, lga, ward, address }
                        .All(string.IsNullOrWhiteSpace)
                        && string.IsNullOrWhiteSpace(dobCell.Text))
                    {
                        continue;
                    }

                    List<string> emptyFields = new();
                    if (string.IsNullOrWhiteSpace(fullName)) emptyFields.Add("FullName");
                    if (string.IsNullOrWhiteSpace(genderValue)) emptyFields.Add("Gender");
                    if (string.IsNullOrWhiteSpace(dobCell.Text)) emptyFields.Add("DateOfBirth");
                    if (string.IsNullOrWhiteSpace(phone)) emptyFields.Add("Phone");
                    if (string.IsNullOrWhiteSpace(ninValue)) emptyFields.Add("NIN");
                    if (string.IsNullOrWhiteSpace(stateValue)) emptyFields.Add("State");
                    if (string.IsNullOrWhiteSpace(lga)) emptyFields.Add("LGA");
                    if (string.IsNullOrWhiteSpace(ward)) emptyFields.Add("Ward");
                    if (string.IsNullOrWhiteSpace(address)) emptyFields.Add("Address");
                    if (emptyFields.Any())
                    {
                        errors.Add(
                            $"Row {rowNumber}: Missing required values: {string.Join(", ", emptyFields)}.");
                        continue;
                    }

                    string gender = genderValue.ToUpperInvariant() switch
                    {
                        "M" or "MALE" => "Male",
                        "F" or "FEMALE" => "Female",
                        _ => string.Empty
                    };
                    if (string.IsNullOrEmpty(gender))
                    {
                        errors.Add($"Row {rowNumber}: Gender must be M, F, Male, or Female.");
                        continue;
                    }

                    DateTime dob;
                    if (dobCell.Value is DateTime excelDate)
                    {
                        dob = excelDate.Date;
                    }
                    else if (!DateTime.TryParseExact(
                        dobCell.Text.Trim(),
                        new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out dob))
                    {
                        errors.Add($"Row {rowNumber}: DateOfBirth must use dd/MM/yyyy.");
                        continue;
                    }

                    if (dob.Date >= DateTime.Today)
                    {
                        errors.Add($"Row {rowNumber}: DateOfBirth must be earlier than today.");
                        continue;
                    }

                    string ninDigits = ninValue.Trim();
                    if (ninDigits.Length != 11
                        || !ninDigits.All(char.IsDigit)
                        || !long.TryParse(ninDigits, out long nin))
                    {
                        errors.Add($"Row {rowNumber}: NIN must contain exactly 11 digits.");
                        continue;
                    }

                    if (!validStates.TryGetValue(stateValue, out string? state))
                    {
                        errors.Add(
                            $"Row {rowNumber}: State must be Adamawa, Bauchi, Borno, Gombe, Taraba, or Yobe.");
                        continue;
                    }

                    if (!NorthEastLocationData.IsValidLga(state, lga))
                    {
                        errors.Add(
                            $"Row {rowNumber}: LGA '{lga}' does not belong to {state}.");
                        continue;
                    }

                    if (!knownNins.Add(nin))
                    {
                        errors.Add(
                            $"Row {rowNumber}: NIN {ninDigits} already exists or is repeated in this file.");
                        continue;
                    }

                    Enrollee enrollee = new()
                    {
                        FullName = fullName,
                        Gender = gender,
                        DateOfBirth = dob,
                        Phone = phone,
                        State = state,
                        LGA = lga,
                        Ward = ward,
                        Address = address,
                        HmoId = hmoId,
                        ProviderId = providerId,
                        NIN = nin,
                        Status = "Active",
                        IsActive = true,
                        DateRegistered = DateTime.Now,
                        RegisteredBy = User.Identity?.Name ?? "Bulk Upload"
                    };

                    nextSequence++;
                    enrollee.EnrollmentNumber =
                        $"CTH-{DateTime.Now:yyyy}-{GetStateCode(state)}-{nextSequence:D6}";

                    enrollees.Add(enrollee);
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            if (errors.Any())
            {
                TempData["Error"] =
                    $"No enrollees were imported because {errors.Count} row error(s) were found.";
                TempData["ErrorDetails"] = string.Join("<br>", errors.Take(20));
            }
            else if (!enrollees.Any())
            {
                TempData["Error"] = "No enrollee rows were found in the workbook.";
            }
            else
            {
                _context.Enrollees.AddRange(enrollees);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync(
                    "Enrollee.BulkUploaded",
                    AuditActor.Format(currentUser, User.Identity?.Name),
                    selectedProvider.Name,
                    AuditActor.Details(
                        $"Imported:{enrollees.Count}",
                        $"HMO:{selectedHmo.Name}",
                        $"Provider:{selectedProvider.Name}",
                        $"File:{excelFile.FileName}"),
                    HttpContext.RequestAborted);
                TempData["Success"] = $"{enrollees.Count} enrollees uploaded successfully!";
                return RedirectAfterEnrollmentChange();

            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "The Excel file could not be processed. Confirm that it uses the downloaded template.";
            TempData["ErrorDetails"] = ex is InvalidDataException ? ex.Message : null;
        }

        return RedirectToAction(nameof(BulkUpload));
    }

    // DELETE GET — SHOW CONFIRMATION
    [Authorize(Roles = "CTSHIPAdmin, HMO")]
    public async Task<IActionResult> Delete(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Encounters)
            .Include(e => e.Claims)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollee == null)
        {
            TempData["Error"] = "Enrollee not found.";
            return RedirectToAction(nameof(Index));
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
    [Authorize(Roles = "CTSHIPAdmin, HMO")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        var enrollee = await _context.Enrollees
            .Include(e => e.Encounters)
            .Include(e => e.Claims)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollee == null)
        {
            TempData["Error"] = "Enrollee not found.";
            return RedirectToAction(nameof(Index));
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

            await _auditService.LogAsync(
                "Enrollee.Deleted",
                AuditActor.Format(currentUser, User.Identity?.Name),
                enrollee.EnrollmentNumber,
                AuditActor.Details(
                    $"Name:{enrollee.FullName}",
                    $"HMO:{enrollee.HmoId}",
                    $"Provider:{enrollee.ProviderId}"),
                HttpContext.RequestAborted);

            TempData["Success"] = $"Enrollee {enrollee.FullName} ({enrollee.EnrollmentNumber}) deleted permanently.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Failed to delete enrollee. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = EnrolleeDashboardRoles)]
    public async Task<IActionResult> Dashboard(
    string search = "",
    string status = "All",
    string state = "All",
    int page = 1,
    int pageSize = 20)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.HmoId == null)
        {
            TempData["Error"] = "Your account is not linked to any HMO.";
            return RedirectToAction("Index", "Home");
        }

        int currentHmoId = currentUser.HmoId.Value;

        var query = _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => e.HmoId == currentHmoId);

        // SEARCH — USE EF.Functions.Like() FOR CASE-INSENSITIVE SEARCH
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.FullName, s) ||
                EF.Functions.Like(e.EnrollmentNumber, s) ||
                EF.Functions.Like(e.Phone, s));
        }

        // FILTER BY STATUS (IsActive boolean)
        if (status == "Active")
            query = query.Where(e => e.Status == "Active");
        else if (status == "Inactive")
            query = query.Where(e => e.Status != "Active");

        // FILTER BY STATE
        if (state != "All" && !string.IsNullOrEmpty(state))
            query = query.Where(e => e.State == state);

        // TOTAL COUNT
        var totalItems = await query.CountAsync();

        // PAGINATION
        var enrollees = await query
            .OrderByDescending(e => e.DateRegistered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // VIEW DATA
        ViewBag.HmoName = await _context.Hmos
            .Where(h => h.Id == currentHmoId)
            .Select(h => h.Name)
            .FirstOrDefaultAsync() ?? "Your HMO";
        ViewBag.TotalEnrollees = totalItems;
        ViewBag.ActiveEnrollees = await _context.Enrollees
            .CountAsync(e => e.HmoId == currentHmoId && e.Status == "Active");
        ViewBag.TotalEncounters = await _context.Encounters
            .CountAsync(e => e.Enrollee != null && e.Enrollee.HmoId == currentHmoId);
        ViewBag.TotalClaims = await _context.Claims
            .CountAsync(e => e.HmoId == currentHmoId);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.State = state;
        ViewBag.CurrentPage = page > 0 ? page : 1;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(enrollees);
    }
}
