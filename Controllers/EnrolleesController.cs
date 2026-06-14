using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using OfficeOpenXml;
using QRCoder;
using System.Diagnostics.Metrics;
using static Bogus.DataSets.Name;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

public class EnrolleesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _hostEnvironment;

    public EnrolleesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
    }

    // INDEX — ALL ENROLLEES
    // GET: /Enrollee or /Enrollee/Index
    [Authorize(Roles = "Admin,HMO")]
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

        ViewBag.StateList = new SelectList(await _context.Enrollees
            .Select(e => e.State).Distinct().OrderBy(s => s).ToListAsync(), state);

        ViewBag.HmoList = new SelectList(await _context.Hmos
            .Select(h => new { h.Id, h.Name })
            .OrderBy(h => h.Name)
            .ToListAsync(), "Id", "Name", hmo);

        return View(model);
    }

    // CREATE
    // GET: Enrollee/Create
    [Authorize(Roles = "Admin,HMO")]
    public async Task<IActionResult> Create()
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
    public async Task<IActionResult> Create(Enrollee enrollee, IFormFile? photo)
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
            // Redirect based on Role
            if (User.IsInRole("Hmo"))
            {
                return RedirectToAction("EnrolleeDashboard", "Hmo");
            }
            else if (User.IsInRole("admin")) 
            {
                return RedirectToAction("Index", "Enrollees");
            }
            return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Edit(int id)
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
    public async Task<IActionResult> Edit(int id, Enrollee enrollee)
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

    // DETAILS
    [Authorize(Roles = "Admin,HMO, Provider")]
    public async Task<IActionResult> Details(int id)
    {
        var enrollee = await _context.Enrollees
            .Include(e => e.Hmo)
            .Include(e => e.MedicalHistories)
            .FirstOrDefaultAsync(e => e.Id == id);
        var currentUser = await _userManager.GetUserAsync(User);
        enrollee.RegisteredBy = currentUser?.Email ?? "Unknown User";
        if (enrollee == null) return NotFound();
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
    private List<SelectListItem> GetNigerianStates()
    {
        var states = new[] {
                "Adamawa", "Bauchi","Borno",
                 "Gombe", "Taraba","Yobe"
            };

        return states.Select(s => new SelectListItem
        {
            Value = s,
            Text = s == "Fct" ? "Abuja" : s
        }).OrderBy(s => s.Text).ToList();
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
                        ProviderId = providerId,
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
                if (User.IsInRole("HMO"))
                {
                    return RedirectToAction("EnrolleeDashboard", "Hmo");
                }
                else if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Enrollees");
                }
                return RedirectToAction(nameof(BulkUpload));

            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error processing file: " + ex.Message;
        }

        return RedirectToAction(nameof(BulkUpload));
    }

    // DELETE GET — SHOW CONFIRMATION
    [Authorize(Roles = "Admin, HMO")]
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
    [Authorize(Roles = "Admin, HMO")]
    public async Task<IActionResult> DeleteConfirmed(int id)
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

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "HMO")]
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

        var query = _context.Enrollees
            .Include(e => e.Hmo)
            .Where(e => e.HmoId == currentUser.HmoId.Value);

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
            query = query.Where(e => e.Status == "Active");

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
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(enrollees);
    }
}