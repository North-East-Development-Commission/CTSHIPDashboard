using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
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
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _hostEnvironment;

    public HmoController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
    }

    // LIST ALL HMOs
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewBag.States = GetNigerianStates();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Hmo hmo, IFormFile? logo)
    {
        ModelState.Remove("LogoPath");

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

            hmo.DateRegistered = DateTime.Now;
            hmo.RegistrationNumber = "HMO-" + DateTime.Now;

            _context.Hmos.Add(hmo);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"HMO '{hmo.Name}' registered successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.States = GetNigerianStates();
        return View(hmo);
    }

    // EDIT HMO
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var hmo = await _context.Hmos.FindAsync(id);
        if (hmo == null) return NotFound();

        ViewBag.States = GetNigerianStates();
        return View(hmo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Hmo hmo, IFormFile? logo)
    {
        if (id != hmo.Id) return NotFound();

        ModelState.Remove("LogoPath");
        ModelState.Remove("RegistrationNumber");
        ModelState.Remove("DateRegistered");

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
                    hmo.LogoPath = "/uploads/hmos/" + fileName;
                }

                _context.Update(hmo);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"HMO '{hmo.Name}' updated successfully!";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Error updating HMO.";
            }
            return RedirectToAction(nameof(Index));
        }

        ViewBag.States = GetNigerianStates();
        return View(hmo);
    }

    // DETAILS
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var hmo = await _context.Hmos
            .Include(h => h.Enrollees)
            .Include(h => h.Claims)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hmo == null) return NotFound();

        ViewBag.CanDelete = (hmo.Enrollees?.Any() != true) && (hmo.Claims?.Any() != true);
        return View(hmo);
    }

    // DELETE POST — SAFE DELETE
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
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
        ViewBag.PendingClaims = hmo.Claims?.Count(c => c.Status == "Submitted" || c.Status == "Review Approved") ?? 0;
        ViewBag.PaidClaims = hmo.Claims?.Count(c => c.Status == "Paid") ?? 0;
        ViewBag.ApprovedClaims = hmo.Claims?.Count(c => c.Status == "Approved") ?? 0;

        ViewBag.Providers = hmo.Providers ?? new List<Provider>();

        return View(hmo);
    }

    // Helper: Nigerian States
    private List<SelectListItem> GetNigerianStates()
    {
        var states = new[] {"Adamawa", "Bauchi","Borno","Gombe","Taraba", "Yobe"};

        return states.Select(s => new SelectListItem
        {
            Value = s,
            Text = s == "FCT" ? "FCT (Abuja)" : s
        }).OrderBy(s => s.Text).ToList();
    }

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

        // Stats for the view
        ViewBag.TotalEncounters = provider.Encounters?.Count ?? 0;
        ViewBag.TotalClaims = provider.Claims?.Count ?? 0;
        ViewBag.TotalClaimAmount = provider.Claims?.Sum(c => c.Amount) ?? 0;

        return View(provider);
    }

    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> EditPro(int? id)
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
    public async Task<IActionResult> EditPro(int id, Provider provider)
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
    public async Task<IActionResult> ProDelete(int? id)
    {
        if (id == null) return NotFound();
        var provider = await _context.Providers.FirstOrDefaultAsync(m => m.Id == id);
        if (provider == null) return NotFound();
        return View(provider);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProDeleteConfirmed(int id)
    {
        var provider = await _context.Providers.FindAsync(id);
        if (provider != null)
        {
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
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

    [Authorize(Roles = "Admin,HMO")]
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

    [Authorize(Roles = "Admin,HMO")]
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
    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> EditEnrollee(int id)
    {
        var enrollee = await _context.Enrollees.FindAsync(id);
        if (enrollee == null) return NotFound();

        ViewBag.States = GetNigerianStates();
        ViewBag.Provider = await _context.Providers.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();

        ViewBag.Hmos = await _context.Hmos.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToListAsync();
        return View(enrollee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEnrollee(int id, Enrollee enrollee)
    {
        if (id != enrollee.Id) return NotFound();

        if (ModelState.IsValid)
        {
            // Handle photo update
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

            _context.Update(enrollee);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Enrollee updated successfully!";
            if (User.IsInRole("HMO"))
            {
                return RedirectToAction("EnrolleeDashboard", "Hmo");
            }
            else if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Enrollees");
            }
            return RedirectToAction(nameof(Index));
        }

        return View(enrollee);
    }

    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> EnrolleeDetails(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .Include(e => e.provider)
            .Include(e => e.MedicalHistories)
            .FirstOrDefaultAsync(e => e.Id == id);
        var currentUser = await _userManager.GetUserAsync(User);
        enrollee.RegisteredBy = currentUser?.Email ?? "Unknown User";
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
    [Authorize(Roles = "Admin,HMO")]
    public IActionResult BulkUpload()
    {
        ViewBag.Hmos = _context.Hmos.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToList();
        ViewBag.Pros = _context.Providers.Select(h => new SelectListItem
        {
            Value = h.Id.ToString(),
            Text = h.Name
        }).ToList();

        return View();
    }

    // POST: Enrollee/BulkUpload
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> BulkUpload(IFormFile excelFile, int hmoId, int providersId)
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