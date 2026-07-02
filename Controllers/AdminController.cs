using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin, Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _context = context;
        }

        public async Task<IActionResult> AuditLogs(DateTime? startDate = null, DateTime? endDate = null, string? action = null, string? performedBy = null, string? export = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                // include whole day
                query = query.Where(l => l.Timestamp <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(l => l.Action == action);
            }
            if (!string.IsNullOrWhiteSpace(performedBy))
            {
                var p = performedBy.Trim();
                query = query.Where(l => l.PerformedBy.Contains(p));
            }

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(5000).ToListAsync();

            // expose filter values to view
            ViewBag.FilterStartDate = startDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ViewBag.FilterEndDate = endDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ViewBag.FilterAction = action ?? string.Empty;
            ViewBag.FilterPerformedBy = performedBy ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(export) && export.ToLower() == "csv")
            {
                // Export CSV
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Timestamp,PerformedBy,Action,TargetUserEmail,Details");
                foreach (var l in logs)
                {
                    var line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                        l.Timestamp.ToString("o"),
                        (l.PerformedBy ?? "").Replace("\"", "\"\""),
                        (l.Action ?? "").Replace("\"", "\"\""),
                        (l.TargetUserEmail ?? "").Replace("\"", "\"\""),
                        (l.Details ?? "").Replace("\"", "\"\""));
                    sb.AppendLine(line);
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                var fileName = $"auditlogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(bytes, "text/csv", fileName);
            }

            return View("AuditLogs2", logs);
        }
        private async Task LogAuditAsync(string action, string targetEmail = null, string details = null)
        {
            var log = new AuditLog
            {
                Action = action,
                PerformedBy = User.Identity?.Name ?? "Unknown",
                TargetUserEmail = targetEmail,
                Details = details,
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        //user activity
        // USER ACTIVITY REPORTS
        public async Task<IActionResult> UserActivity()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            var activities = await _context.UserActivities
                .Include(a => a.User)
                .Where(a => a.Timestamp >= thirtyDaysAgo)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            var userStats = await _context.Users
                .Select(u => new UserActivityReportViewModel
                {
                    UserEmail = u.Email!,
                    LastLogin = _context.UserActivities
                        .Where(a => a.UserId == u.Id && a.Action == "Login")
                        .OrderByDescending(a => a.Timestamp)
                        .Select(a => a.Timestamp)
                        .FirstOrDefault(),
                    TotalLogins = _context.UserActivities
                        .Count(a => a.UserId == u.Id && a.Action == "Login"),
                    LastSeen = _context.UserActivities
                        .Where(a => a.UserId == u.Id)
                        .OrderByDescending(a => a.Timestamp)
                        .Select(a => a.Timestamp)
                        .FirstOrDefault()
                })
                .OrderByDescending(u => u.LastSeen)
                .ToListAsync();

            var model = new UserActivityDashboardViewModel
            {
                RecentActivities = activities.Take(50).ToList(),
                UserStats = userStats,
                TotalLoginsToday = activities.Count(a => a.Timestamp.Date == DateTime.Today && a.Action == "Login"),
                ActiveUsersLast7Days = userStats.Count(u => u.LastSeen >= DateTime.Now.AddDays(-7))
            };

            return View(model);
        }

        // FINANCIAL: Disburse monthly allocation to enrollees
        [HttpPost]
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> DisburseMonthly(decimal amountPerEnrollee)
        {
            if (amountPerEnrollee <= 0)
            {
                TempData["Error"] = "Amount per enrollee must be greater than zero.";
                return RedirectToAction(nameof(Users));
            }

            var enrollees = await _context.Enrollees.ToListAsync();
            int count = 0;
            foreach (var e in enrollees)
            {
                var wallet = await _context.EnrolleeWallets.FirstOrDefaultAsync(w => w.EnrolleeId == e.Id);
                if (wallet == null)
                {
                    wallet = new EnrolleeWallet
                    {
                        EnrolleeId = e.Id,
                        Balance = amountPerEnrollee,
                        MonthlyAllocation = amountPerEnrollee,
                        LastDisbursedAt = DateTime.UtcNow
                    };
                    _context.EnrolleeWallets.Add(wallet);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    wallet.Balance += amountPerEnrollee;
                    wallet.MonthlyAllocation = amountPerEnrollee;
                    wallet.LastDisbursedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                _context.WalletTransactions.Add(new WalletTransaction
                {
                    EnrolleeWalletId = wallet.Id,
                    Amount = amountPerEnrollee,
                    Type = "Disburse",
                    Reference = "Monthly Allocation",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                count++;
            }

            // Audit the bulk disbursement
            await LogAuditAsync("AdminBulkDisbursement", null, $"AmountPerEnrollee: {amountPerEnrollee:C}; Count: {count}");

            TempData["Success"] = $"Monthly allocation disbursed to {count} enrollees (amount: {amountPerEnrollee:C}).";
            return RedirectToAction(nameof(Users));
        }


        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> LogLogin()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    _context.UserActivities.Add(new UserActivity
                    {
                        UserId = user.Id,
                        Action = "Login",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        DeviceInfo = Request.Headers["User-Agent"].ToString(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }
            return Ok();
        }


        // ========================================
        // 1. USER LIST VIEW
        // ========================================
        public async Task<IActionResult> Users(string search = "")
        {
            // Use .Include to eager-load navigation properties
            var query = _userManager.Users
                .Include(u => u.Organizations)
                .Include(u => u.Provider)
                .Include(u => u.hmo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                    u.Email.ToLower().Contains(search));
            }

            var userList = await query.OrderBy(u => u.FullName).ToListAsync();
            var model = new List<UserViewModel>();

            foreach (var user in userList)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add(new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "Not Set",
                    Email = user.Email!,
                    // FIX: Removed the '?' and correctly mapped the ID
                    OrganizationId = user.OrganizationId ?? 0,
                    // Pass the loaded object to the viewmodel
                    organization = user.Organizations,
                    Roles = roles.ToList(),
                    State = user.State,
                    ContactInfo = user.ContactInfo,
                    IsLocked = await _userManager.IsLockedOutAsync(user)
                });
            }

        ViewBag.Search = search;
            return View(model);
        }

        // ========================================
        // 2. CREATE NEW ROLE (GET & POST)
        // ========================================
        [HttpGet]
        public IActionResult CreateRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("", "Role name is required.");
                return View();
            }

            roleName = roleName.Trim();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError("", $"Role '{roleName}' already exists!");
                return View();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));

            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{roleName}' created successfully!";
                return RedirectToAction(nameof(Roles));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        // ========================================
        // 3. LIST ALL ROLES
        // ========================================
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            return View(roles);
        }

        // ========================================
        // 4. ASSIGN ROLE (MULTIPLE ROLES)
        // ========================================
        [HttpGet]
        public async Task<IActionResult> AssignRole(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

            var model = new AssignRoleViewModel
            {
                UserId = user.Id,
                FullName = user.FullName ?? "Not Set",
                Email = user.Email!,
                CurrentRoles = userRoles.ToList(),
                AllRoles = allRoles,
                SelectedRoles = userRoles.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);

            var toRemove = currentRoles.Except(model.SelectedRoles ?? new List<string>());
            var toAdd = (model.SelectedRoles ?? new List<string>()).Except(currentRoles);

            if (toRemove.Any())
                await _userManager.RemoveFromRolesAsync(user, toRemove);

            if (toAdd.Any())
                await _userManager.AddToRolesAsync(user, toAdd);

            // Audit: roles changed
            try
            {
                var added = string.Join(',', toAdd);
                var removed = string.Join(',', toRemove);
                await LogAuditAsync("RolesUpdated", user.Email, $"Added:{added}; Removed:{removed}");
            }
            catch { }

            TempData["Success"] = $"Roles updated for {user.FullName}";
            return RedirectToAction(nameof(Users));
        }

        // CREATE USER
        // GET: Admin/CreateUser
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> CreateUser()
        {
            await PopulateDropdownsAsync();

            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            ViewBag.AllRoles = roles.Select(r => new SelectListItem
            {
                Value = r.Name,
                Text = GetFriendlyRoleName(r.Name)
            }).ToList();
            return View(new CreateUserViewModel());
        }

        // POST: Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    State = model.State,
                    ContactInfo = model.ContactInfo,
                    EmailConfirmed = true,
                    // --- ADD THESE LINES TO CAPTURE THE IDS ---
                    OrganizationId = model.OrganizationId, // Maps to the OrganizeId foreign key
                    ProviderId = model.ProviderId,
                    HmoId = model.HmoId
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    if (model.SelectedRoles != null && model.SelectedRoles.Any())
                    {
                        foreach (var role in model.SelectedRoles)
                        {
                            await _userManager.AddToRoleAsync(user, role);
                        }
                    }

                    // Audit: new user created
                    try
                    {
                        await LogAuditAsync("UserCreated", user.Email, $"Created user: {model.FullName}; Roles: {string.Join(',', model.SelectedRoles ?? new List<string>())}");
                    }
                    catch { }

                    TempData["Success"] = $"User '{model.FullName}' created successfully!";
                    return RedirectToAction("Users", "Admin");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            await PopulateDropdownsAsync();
            return View(model);
        }


        // EDIT USER
        public async Task<IActionResult> EditUser(string id)
        {
            await PopulateDropdownsAsync();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "",
                Email = user.Email!,
                Organization = user.Organizations,
                ContactInfo = user.ContactInfo,
                State = user.State,
                // --- ADD THESE LINES TO CAPTURE THE IDS ---
                OrganizationId = user.OrganizationId, // Maps to the OrganizeId foreign key
                ProviderId = user.ProviderId,
                HmoId = user.HmoId,
                CurrentRoles = userRoles.ToList(),
                AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync()
            };

            ViewBag.States = GetNigerianStates(); // Your Nigerian states method

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.State = model.State;
            user.ContactInfo = model.ContactInfo;
            user.OrganizationId = model.OrganizationId;
            user.ProviderId = model.ProviderId; // Captured from the dropdown shown by JS
            user.HmoId = model.HmoId;

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Except(model.SelectedRoles ?? new List<string>());
            var rolesToAdd = (model.SelectedRoles ?? new List<string>()).Except(currentRoles);

            if (rolesToRemove.Any()) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (rolesToAdd.Any()) await _userManager.AddToRolesAsync(user, rolesToAdd);

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "User updated successfully!";

            await PopulateDropdownsAsync();
            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/DeleteUser/5
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.UserName = user.FullName ?? user.Email;
            return View(user);
        }

        // POST: Admin/DeleteUser/5
        [HttpPost, ActionName("DeleteUserConfirmed")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Users");
            }

            // STEP 1: Remove user from all roles first (removes rows from AspNetUserRoles)
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any())
            {
                var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, userRoles);
                if (!removeRolesResult.Succeeded)
                {
                    TempData["Error"] = "Failed to remove user roles.";
                    return RedirectToAction("Users");
                }
            }

            // STEP 2: Now safely delete the user
            var deleteResult = await _userManager.DeleteAsync(user);
            if (deleteResult.Succeeded)
            {
                // Audit: user deleted
                try
                {
                    await LogAuditAsync("UserDeleted", user.Email, $"Deleted user: {user.FullName ?? user.Email}");
                }
                catch { }

                TempData["Success"] = $"User '{user.FullName ?? user.Email}' has been deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to delete user: " + string.Join(", ", deleteResult.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Users");
        }

        // HELPER METHOD — POPULATE BOTH DROPDOWNS
        private async Task PopulateDropdownsAsync()
        {
            // ROLES — Now uses STATIC method → No memory leak
            ViewBag.AllRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Name,
                    Text = GetFriendlyRoleName(r.Name)  // Static method = SAFE
                })
                .ToListAsync();

            ViewBag.States = GetNigerianStates();

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
            ViewBag.Oga = await _context.Organizations.Select(h => new SelectListItem
            {
                Value = h.Id.ToString(),
                Text = h.Name
            }).ToListAsync();
        }

        // MAKE IT STATIC!
        private static string GetFriendlyRoleName(string role) => role switch
        {
            "CTSHIPAdmin" => "System Administrator",
            "HMO" => "HMO Officer",
            "Provider" => "Hospital/Provider Staff",
            "Reviewer" => "Claims Reviewer",
            "Auditor" => "Internal Auditor",
            "Finance" => "Finance Officer",
            _ => role
        };


        private List<SelectListItem> GetNigerianStates()
        {
            var states = new[] { "Borno", "Adamawa", "Taraba", "Yobe", "Bauchi", "Gombe" };

            return states.Select(s => new SelectListItem { Value = s, Text = s == "FCT" ? "FCT (Abuja)" : s })
                         .OrderBy(s => s.Text)
                         .ToList();
        }



        // ========================================
        // 5. SECURE LOGOUT
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["Success"] = "Logged out securely.";
            return RedirectToAction("Index", "Home");
        }

        // Optional: Make role names user-friendly

        private async Task PopulateRoleViewBagsAsync(ApplicationUser? user = null)
        {
            var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.AllRoles = allRoles;

            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                ViewBag.CurrentRoles = userRoles.Any() ? userRoles.ToList() : new List<string>();
            }
            else
            {
                ViewBag.CurrentRoles = new List<string>();
            }
        }


        [Authorize(Roles = "Admin,HMO,Provider")]
        public async Task<IActionResult> EnrolleesPerProvider(
        int? providerId = null,
        string search = "",
        int page = 1,
        int pageSize = 20)
        {
            // Get all providers for dropdown
            var providers = await _context.Providers
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            ViewBag.Providers = new SelectList(providers, "Id", "Name", providerId);

            // Base query
            var query = _context.Encounters
                .Include(e => e.Enrollee)
                .Include(e => e.Provider)
                .AsQueryable();

            // Filter by selected provider
            if (providerId.HasValue)
            {
                query = query.Where(e => e.ProviderId == providerId.Value);
            }

            // Search by enrollee name, ID, or phone
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = $"%{search.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.Like(e.Enrollee.FullName, s) ||
                    EF.Functions.Like(e.Enrollee.EnrollmentNumber, s) ||
                    EF.Functions.Like(e.Enrollee.Phone, s));
            }

            // Get unique enrollees (avoid duplicates if multiple encounters)
            var enrolleeIds = await query
                .Select(e => e.EnrolleeId)
                .Distinct()
                .ToListAsync();

            var totalItems = enrolleeIds.Count;

            var enrolleeIdsPaged = enrolleeIds
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var enrollees = await _context.Enrollees
                .Include(e => e.Hmo)
                .Where(e => enrolleeIdsPaged.Contains(e.Id))
                .OrderBy(e => e.FullName)
                .ToListAsync();

            // Provider name for title
            string providerName = "All Providers";
            if (providerId.HasValue)
            {
                var provider = await _context.Providers.FindAsync(providerId);
                providerName = provider?.Name ?? "Unknown Provider";
            }

            ViewBag.ProviderName = providerName;
            ViewBag.TotalEnrollees = totalItems;
            ViewBag.Search = search;
            ViewBag.SelectedProviderId = providerId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(enrollees);
        }

        // GET: /Admin/CreateStateOfficer
        public IActionResult CreateStateOfficer()
        {
            ViewBag.States = SeedDataHelper.GetNigerianStates(); // small helper below in ViewModels file
            return View(new CreateStateOfficerViewModel());
        }

        // POST: /Admin/CreateStateOfficer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStateOfficer(CreateStateOfficerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.States = SeedDataHelper.GetNigerianStates();
                return View(model);
            }

            // Ensure role exists
            if (!await _roleManager.RoleExistsAsync("StateOffice"))
            {
                await _roleManager.CreateAsync(new IdentityRole("StateOffice"));
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("", "A user with this email already exists.");
                ViewBag.States = SeedDataHelper.GetNigerianStates();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                ContactInfo = model.Phone,
                State = model.State,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors) ModelState.AddModelError("", err.Description);
                ViewBag.States = SeedDataHelper.GetNigerianStates();
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "StateOffice");
            if (!roleResult.Succeeded)
            {
                foreach (var err in roleResult.Errors) ModelState.AddModelError("", err.Description);
                ViewBag.States = SeedDataHelper.GetNigerianStates();
                return View(model);
            }

            TempData["Success"] = $"State Officer created for {model.State}: {model.Email}";
            return RedirectToAction("CreateStateOfficer");
        }
    }
}
    