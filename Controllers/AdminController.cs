using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin,Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
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
                .Where(u => !u.IsDeleted)
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
                    OrganizationId = user.OrganizationId ?? 0,
                    organization = user.Organizations,
                    ProviderId = user.ProviderId,
                    Provider = user.Provider,
                    HmoId = user.HmoId,
                    hmo = user.hmo,
                    Roles = roles.ToList(),
                    State = user.State,
                    ContactInfo = user.ContactInfo,
                    EmailConfirmed = user.EmailConfirmed,
                    IsLocked = await _userManager.IsLockedOutAsync(user),
                    IsDeleted = user.IsDeleted,
                    DeletedAt = user.DeletedAt,
                    DeletedByName = user.DeletedByName,
                    DeletionReason = user.DeletionReason
                });
            }

            ViewBag.Search = search;
            return View(model);
        }

        public async Task<IActionResult> UserDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var user = await _userManager.Users
                .Include(u => u.Organizations)
                .Include(u => u.Provider)
                .Include(u => u.hmo)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "Not Set",
                Email = user.Email ?? string.Empty,
                OrganizationId = user.OrganizationId ?? 0,
                organization = user.Organizations,
                ProviderId = user.ProviderId,
                Provider = user.Provider,
                HmoId = user.HmoId,
                hmo = user.hmo,
                Roles = roles.ToList(),
                State = user.State,
                ContactInfo = user.ContactInfo,
                EmailConfirmed = user.EmailConfirmed,
                IsLocked = await _userManager.IsLockedOutAsync(user),
                IsDeleted = user.IsDeleted,
                DeletedAt = user.DeletedAt,
                DeletedByName = user.DeletedByName,
                DeletionReason = user.DeletionReason
            };

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
            if (user == null || user.IsDeleted) return NotFound();

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
            if (user == null || user.IsDeleted) return NotFound();

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
            await ValidateAndNormalizeOrganizationLinksAsync(
                model.OrganizationId,
                model.ProviderId,
                model.HmoId,
                model.ReferralHospitalId,
                providerId => model.ProviderId = providerId,
                hmoId => model.HmoId = hmoId,
                referralHospitalId => model.ReferralHospitalId = referralHospitalId,
                RequiresHmoScope(model.SelectedRoles),
                RequiresReferralScope(model.SelectedRoles));

            if (ModelState.IsValid)
            {
                                string normalizedEmail = _userManager.NormalizeEmail(model.Email!);
                ApplicationUser? deletedUser = await _userManager.Users
                    .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail && user.IsDeleted);

                if (deletedUser != null)
                {
                    deletedUser.UserName = model.Email;
                    deletedUser.NormalizedUserName = _userManager.NormalizeName(model.Email!);
                    deletedUser.Email = model.Email;
                    deletedUser.NormalizedEmail = normalizedEmail;
                    deletedUser.FullName = model.FullName;
                    deletedUser.State = model.State;
                    deletedUser.ContactInfo = model.ContactInfo;
                    deletedUser.EmailConfirmed = true;
                    deletedUser.OrganizationId = model.OrganizationId;
                    deletedUser.ProviderId = model.ProviderId;
                    deletedUser.HmoId = model.HmoId;
                    deletedUser.IsDeleted = false;
                    deletedUser.DeletedAt = null;
                    deletedUser.DeletedByUserId = null;
                    deletedUser.DeletedByName = null;
                    deletedUser.DeletionReason = null;
                    deletedUser.LockoutEnd = null;
                    deletedUser.LockoutEnabled = true;

                    var passwordToken = await _userManager.GeneratePasswordResetTokenAsync(deletedUser);
                    var passwordResult = await _userManager.ResetPasswordAsync(deletedUser, passwordToken, model.Password!);
                    if (!passwordResult.Succeeded)
                    {
                        foreach (var error in passwordResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        await PopulateDropdownsAsync();
                        return View(model);
                    }

                    var updateResult = await _userManager.UpdateAsync(deletedUser);
                    if (!updateResult.Succeeded)
                    {
                        foreach (var error in updateResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        await PopulateDropdownsAsync();
                        return View(model);
                    }

                    var currentRoles = await _userManager.GetRolesAsync(deletedUser);
                    if (currentRoles.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(deletedUser, currentRoles);
                    }

                    if (model.SelectedRoles != null && model.SelectedRoles.Any())
                    {
                        await _userManager.AddToRolesAsync(deletedUser, model.SelectedRoles);
                    }

                    await _userManager.UpdateSecurityStampAsync(deletedUser);
                    await LogAuditAsync("UserRestored", deletedUser.Email, $"Restored safe-deleted user: {model.FullName}; Roles: {string.Join(',', model.SelectedRoles ?? new List<string>())}");
                    TempData["Success"] = $"User '{model.FullName}' has been re-added successfully.";
                    return RedirectToAction(nameof(Users));
                }

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
                    return RedirectToAction(nameof(Users));
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
            if (user == null || user.IsDeleted) return NotFound();

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
                ReferralHospitalId = await GetReferralHospitalIdForProviderAsync(user.ProviderId),
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
            if (user == null || user.IsDeleted) return NotFound();

            await ValidateAndNormalizeOrganizationLinksAsync(
                model.OrganizationId,
                model.ProviderId,
                model.HmoId,
                model.ReferralHospitalId,
                providerId => model.ProviderId = providerId,
                hmoId => model.HmoId = hmoId,
                referralHospitalId => model.ReferralHospitalId = referralHospitalId,
                RequiresHmoScope(model.SelectedRoles),
                RequiresReferralScope(model.SelectedRoles));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                model.CurrentRoles = (await _userManager.GetRolesAsync(user)).ToList();
                model.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(model);
            }

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
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound();

            ViewBag.UserName = user.FullName ?? user.Email;
            return View(user);
        }

        // POST: Admin/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin,Admin")]
        public async Task<IActionResult> DeleteUser(string id, string confirmDelete, string? deletionReason)
        {
            if (string.IsNullOrEmpty(id) || confirmDelete != "true") return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.IsDeleted)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == user.Id)
            {
                TempData["Error"] = "You cannot safe-delete your own active account.";
                return RedirectToAction(nameof(Users));
            }

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedByUserId = currentUser?.Id;
            user.DeletedByName = currentUser?.FullName ?? User.Identity?.Name;
            user.DeletionReason = string.IsNullOrWhiteSpace(deletionReason) ? null : deletionReason.Trim();
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Failed to safe-delete user: " + string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            await _userManager.UpdateSecurityStampAsync(user);
            await LogAuditAsync(
                "UserSafeDeleted",
                user.Email,
                $"Safe-deleted user: {user.FullName ?? user.Email}. Activity and audit history retained. Reason: {user.DeletionReason ?? "Not supplied"}");

            TempData["Success"] = $"User '{user.FullName ?? user.Email}' has been safe-deleted. Activity history was retained.";
            return RedirectToAction(nameof(Users));
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
            ViewBag.Provider = await _context.Providers
                .Where(h => h.IsActive)
                .OrderBy(h => h.Name)
                .Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = h.Name
                })
                .ToListAsync();
            ViewBag.ReferralHospitals = await _context.ReferralHospitals
                .Where(h => h.IsActive)
                .Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(h.State) ? h.Name : h.Name + " - " + h.State
                })
                .OrderBy(x => x.Text)
                .ToListAsync();
            ViewBag.Oga = await _context.Organizations.Select(h => new SelectListItem
            {
                Value = h.Id.ToString(),
                Text = h.Name
            }).ToListAsync();
        }

        [HttpGet]
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> GetProvidersByHmo(int hmoId, int? selectedProviderId = null)
        {
            if (hmoId <= 0)
            {
                return Json(new { success = false, providers = Array.Empty<object>() });
            }

            var providers = await _context.Providers
                .Where(provider => provider.IsActive && provider.HmoId == hmoId)
                .OrderBy(provider => provider.Name)
                .Select(provider => new
                {
                    id = provider.Id,
                    text = provider.Name + " - " + provider.State,
                    selected = selectedProviderId.HasValue && provider.Id == selectedProviderId.Value
                })
                .ToListAsync();

            return Json(new { success = true, providers });
        }

        [HttpGet]
        [Authorize(Roles = "CTSHIPAdmin")]
        public async Task<IActionResult> GetReferralProvidersByHmo(int hmoId, Guid? selectedReferralHospitalId = null)
        {
            bool hmoExists = await _context.Hmos.AnyAsync(hmo => hmo.Id == hmoId);
            if (!hmoExists)
            {
                return Json(new { success = false, providers = Array.Empty<object>() });
            }

            var providers = await _context.ReferralHospitals
                .Where(hospital => hospital.IsActive)
                .OrderBy(hospital => hospital.Name)
                .Select(hospital => new
                {
                    id = hospital.Id,
                    text = string.IsNullOrWhiteSpace(hospital.State)
                        ? hospital.Name
                        : hospital.Name + " - " + hospital.State,
                    selected = selectedReferralHospitalId.HasValue && hospital.Id == selectedReferralHospitalId.Value
                })
                .ToListAsync();

            return Json(new { success = true, providers });
        }

        private async Task ValidateAndNormalizeOrganizationLinksAsync(
            int? organizationId,
            int? providerId,
            int? hmoId,
            Guid? referralHospitalId,
            Action<int?> setProviderId,
            Action<int?> setHmoId,
            Action<Guid?> setReferralHospitalId,
            bool requiresHmoScope = false,
            bool requiresReferralScope = false)
        {
            if (!organizationId.HasValue)
            {
                setProviderId(null);
                setReferralHospitalId(null);
                if (requiresReferralScope)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.OrganizationId), "Select the referral organization type for this ReferralPro user.");
                    if (!hmoId.HasValue)
                    {
                        ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select the HMO for this referral provider.");
                    }
                    if (!referralHospitalId.HasValue)
                    {
                        ModelState.AddModelError(nameof(CreateUserViewModel.ReferralHospitalId), "Select a referral provider.");
                    }
                }
                if (!requiresHmoScope)
                {
                    setHmoId(null);
                }
                else if (!hmoId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select an HMO for this role.");
                }
                return;
            }

            string? organizationName = await _context.Organizations
                .Where(organization => organization.Id == organizationId.Value)
                .Select(organization => organization.Name)
                .FirstOrDefaultAsync();

            if (organizationName == null)
            {
                ModelState.AddModelError(nameof(CreateUserViewModel.OrganizationId), "Select a valid organization type.");
                setProviderId(null);
                setHmoId(null);
                setReferralHospitalId(null);
                return;
            }

            if (IsReferralOrganization(organizationName) || requiresReferralScope)
            {
                if (!hmoId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select the HMO for this referral provider.");
                }

                if (!referralHospitalId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.ReferralHospitalId), "Select a referral provider.");
                }

                if (hmoId.HasValue && referralHospitalId.HasValue)
                {
                    int? referralProviderId = await GetOrCreateReferralProviderAsync(referralHospitalId.Value, hmoId.Value);
                    if (referralProviderId.HasValue)
                    {
                        setProviderId(referralProviderId.Value);
                    }
                    else
                    {
                        ModelState.AddModelError(nameof(CreateUserViewModel.ReferralHospitalId), "Select an active referral provider.");
                        setProviderId(null);
                    }
                }
                else
                {
                    setProviderId(null);
                }

                setReferralHospitalId(referralHospitalId);
                return;
            }

            setReferralHospitalId(null);

            if (requiresHmoScope)
            {
                setProviderId(null);
                if (!hmoId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select an HMO for this role.");
                }
                return;
            }

            if (IsProviderOrganization(organizationName))
            {
                if (!hmoId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select the HMO before selecting a provider.");
                }

                if (!providerId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.ProviderId), "Select a provider.");
                }
                else if (hmoId.HasValue
                    && !await _context.Providers.AnyAsync(provider =>
                        provider.Id == providerId.Value &&
                        provider.HmoId == hmoId.Value &&
                        provider.IsActive))
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.ProviderId), "Select a provider under the selected HMO.");
                }

                return;
            }

            if (IsHmoOrganization(organizationName))
            {
                setProviderId(null);
                if (!hmoId.HasValue)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.HmoId), "Select an HMO.");
                }
                return;
            }

            setProviderId(null);
            setHmoId(null);
        }

        private async Task<int?> GetOrCreateReferralProviderAsync(Guid referralHospitalId, int hmoId)
        {
            ReferredHospital? hospital = await _context.ReferralHospitals
                .FirstOrDefaultAsync(x => x.Id == referralHospitalId && x.IsActive);
            if (hospital == null)
            {
                return null;
            }

            string providerCode = BuildReferralProviderCode(referralHospitalId, hmoId);
            Provider? provider = await _context.Providers.FirstOrDefaultAsync(x =>
                x.HmoId == hmoId &&
                (x.Code == providerCode ||
                 x.Name == hospital.Name ||
                 (!string.IsNullOrWhiteSpace(hospital.Email) && x.Email == hospital.Email)));

            if (provider != null)
            {
                provider.IsActive = true;
                provider.Code = string.IsNullOrWhiteSpace(provider.Code) ? providerCode : provider.Code;
                provider.Level = string.IsNullOrWhiteSpace(provider.Level) ? "Referral Hospital" : provider.Level;
                provider.State = string.IsNullOrWhiteSpace(provider.State) ? hospital.State ?? "N/A" : provider.State;
                provider.LGA = string.IsNullOrWhiteSpace(provider.LGA) ? hospital.Lga ?? "N/A" : provider.LGA;
                provider.Location = string.IsNullOrWhiteSpace(provider.Location) ? hospital.Address : provider.Location;
                provider.Phone = string.IsNullOrWhiteSpace(provider.Phone) ? hospital.PhoneNumber ?? "N/A" : provider.Phone;
                provider.Email = string.IsNullOrWhiteSpace(provider.Email) ? hospital.Email ?? string.Empty : provider.Email;
                await _context.SaveChangesAsync();
                return provider.Id;
            }

            provider = new Provider
            {
                Name = hospital.Name,
                Location = hospital.Address,
                IsActive = true,
                PatientRatio = 0,
                State = string.IsNullOrWhiteSpace(hospital.State) ? "N/A" : hospital.State,
                LGA = string.IsNullOrWhiteSpace(hospital.Lga) ? "N/A" : hospital.Lga,
                Phone = string.IsNullOrWhiteSpace(hospital.PhoneNumber) ? "N/A" : hospital.PhoneNumber,
                Email = hospital.Email ?? string.Empty,
                Code = providerCode,
                Level = "Referral Hospital",
                DateRegistered = DateTime.Now,
                HmoId = hmoId
            };

            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();
            return provider.Id;
        }

        private async Task<Guid?> GetReferralHospitalIdForProviderAsync(int? providerId)
        {
            if (!providerId.HasValue)
            {
                return null;
            }

            Provider? provider = await _context.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == providerId.Value);
            if (provider == null)
            {
                return null;
            }

            return await _context.ReferralHospitals
                .AsNoTracking()
                .Where(hospital => hospital.IsActive &&
                    (hospital.Name == provider.Name ||
                     (!string.IsNullOrWhiteSpace(hospital.Email) && hospital.Email == provider.Email)))
                .Select(hospital => (Guid?)hospital.Id)
                .FirstOrDefaultAsync();
        }

        private static string BuildReferralProviderCode(Guid referralHospitalId, int hmoId)
        {
            return $"REF-{hmoId}-{referralHospitalId:N}"[..18].ToUpperInvariant();
        }

        private static bool IsProviderOrganization(string organizationName)
        {
            return organizationName.Contains("provider", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReferralOrganization(string organizationName)
        {
            return organizationName.Contains("referral", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHmoOrganization(string organizationName)
        {
            return organizationName.Contains("health maintenance", StringComparison.OrdinalIgnoreCase)
                || organizationName.Contains("hmo", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresHmoScope(IEnumerable<string>? roles)
        {
            return roles?.Any(role =>
                string.Equals(role, "Reviewer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "HMO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "HmoEnrollmentOfficer", StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static bool RequiresReferralScope(IEnumerable<string>? roles)
        {
            return roles?.Any(role =>
                string.Equals(role, "ReferralPro", StringComparison.OrdinalIgnoreCase)) == true;
        }

        // MAKE IT STATIC!
        private static string GetFriendlyRoleName(string role) => role switch
        {
            "CTSHIPAdmin" => "System Administrator",
            "HMO" => "HMO Officer",
            "HmoEnrollmentOfficer" => "HMO Enrollment Officer",
            "Provider" => "Hospital/Provider Staff",
            "ReferralPro" => "Referral Provider",
            "Reviewer" => "Claims Reviewer",
            "Auditor" => "Internal Auditor",
            "Finance" => "Finance Officer",
            _ => role
        };


        private List<SelectListItem> GetNigerianStates()
        {
            return StateSelectListHelper.NorthEastStates();
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


        [Authorize(Roles = "CTSHIPAdmin,Admin,HMO,Provider")]
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
    }
}
    
