// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace CTSHIPDashboard.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private const string LocalAdminEmail =
            "as.maiwada@nedc.gov.ng";

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(
                    string.Empty,
                    ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(
            string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // ====================================================
            // NORMALIZE EMAIL ADDRESS
            // ====================================================

            var email =
                Input.Email?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return Page();
            }

            // ====================================================
            // FIND USER BY EMAIL
            // ====================================================

            var user =
                await _userManager.FindByEmailAsync(
                    email);

            if (user == null)
            {
                _logger.LogWarning(
                    "Login attempt for unknown email address {Email}.",
                    email);

                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return Page();
            }

            // ====================================================
            // PASSWORD DIAGNOSTIC
            //
            // This temporarily helps determine whether the password
            // entered by the user matches the Identity password hash.
            //
            // The password itself is NEVER written to the logs.
            // ====================================================

            var passwordValid =
                await _userManager.CheckPasswordAsync(
                    user,
                    Input.Password);

            _logger.LogInformation(
                "LOGIN TEST - User: {Email}, UserId: {UserId}, PasswordValid: {PasswordValid}",
                user.Email,
                user.Id,
                passwordValid);

            // ====================================================
            // PASSWORD SIGN IN
            // ====================================================

            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: false);

            // ====================================================
            // LOG COMPLETE SIGN-IN RESULT
            // ====================================================

            _logger.LogInformation(
                "LOGIN TEST - User: {Email}, UserId: {UserId}, " +
                "Succeeded: {Succeeded}, LockedOut: {LockedOut}, " +
                "NotAllowed: {NotAllowed}, RequiresTwoFactor: {RequiresTwoFactor}",
                user.Email,
                user.Id,
                result.Succeeded,
                result.IsLockedOut,
                result.IsNotAllowed,
                result.RequiresTwoFactor);

            // ====================================================
            // SUCCESSFUL SIGN IN
            // ====================================================

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "User {Email} ({UserId}) logged in successfully.",
                    user.Email,
                    user.Id);

                // =================================================
                // ENSURE LOCAL ADMIN ROLES
                // =================================================

                if (IsLocalAdmin(user))
                {
                    await EnsureLocalAdminRolesAsync(
                        user);

                    // Refresh authentication cookie so newly added
                    // roles are immediately available.
                    await _signInManager.RefreshSignInAsync(
                        user);
                }

                // =================================================
                // AUDIT LOGIN
                //
                // Failure to save audit records must NEVER stop
                // a successfully authenticated user from logging in.
                // =================================================

                try
                {
                    var now =
                        DateTime.Now;

                    var actor =
                        string.IsNullOrWhiteSpace(
                            user.FullName)
                            ? user.Email
                              ?? user.UserName
                              ?? user.Id
                            : $"{user.FullName} ({user.Email ?? user.UserName})";

                    var ipAddress =
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString();

                    var deviceInfo =
                        Request.Headers
                            .UserAgent
                            .ToString();

                    _context.UserActivities.Add(
                        new UserActivity
                        {
                            UserId = user.Id,
                            Action = "Login",
                            IpAddress = ipAddress,
                            DeviceInfo = deviceInfo,
                            Timestamp = now
                        });

                    _context.AuditLogs.Add(
                        new AuditLog
                        {
                            Action = "Account.Login",
                            PerformedBy = actor,
                            Details =
                                $"Successful login; IP {ipAddress}",
                            Timestamp = now
                        });

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Login audit logging failed for user {Email} ({UserId}).",
                        user.Email,
                        user.Id);
                }

                // =================================================
                // GET USER ROLES
                // =================================================

                var roles =
                    await _userManager.GetRolesAsync(
                        user);

                var roleSet =
                    roles.ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "Authenticated user {Email} ({UserId}) has roles: {Roles}",
                    user.Email,
                    user.Id,
                    string.Join(", ", roles));

                // =================================================
                // ROLE-BASED REDIRECTION
                // Keep consistent with Program.cs
                // =================================================

                if (roleSet.Contains("CTSHIPAdmin") ||
                    roleSet.Contains("Admin"))
                {
                    return RedirectToAction(
                        "Index",
                        "Analytics",
                        new { area = "" });
                }

                if (roleSet.Contains("HmoEnrollmentOfficer"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Enrollees",
                        new { area = "" });
                }

                if (roleSet.Contains("HMO"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Hmo",
                        new { area = "" });
                }

                if (roleSet.Contains("ReferralPro"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "ReferralPro",
                        new { area = "" });
                }

                if (roleSet.Contains("Provider"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Providers",
                        new { area = "" });
                }

                if (roleSet.Contains("Finance"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Finance",
                        new { area = "" });
                }

                if (roleSet.Contains("StateOffice"))
                {
                    return RedirectToAction(
                        "Index",
                        "StateOffice",
                        new { area = "" });
                }

                if (roleSet.Contains("NHIA"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "NHIA",
                        new { area = "" });
                }

                if (roleSet.Contains("SSHIA"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "SSHIA",
                        new { area = "" });
                }

                if (roleSet.Contains("IHSA") ||
                    roleSet.Contains("NEDCAdmin"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "IHSA",
                        new { area = "" });
                }

                if (roleSet.Contains("Monitoring"))
                {
                    return RedirectToAction(
                        "Index",
                        "Monitoring",
                        new { area = "" });
                }

                // User authenticated successfully but no recognized
                // CTSHIP role was assigned.
                _logger.LogWarning(
                    "User {Email} ({UserId}) authenticated successfully but has no recognized CTSHIP role. Roles: {Roles}",
                    user.Email,
                    user.Id,
                    string.Join(", ", roles));

                return LocalRedirect(returnUrl);
            }

            // ====================================================
            // TWO-FACTOR AUTHENTICATION
            // ====================================================

            if (result.RequiresTwoFactor)
            {
                _logger.LogInformation(
                    "Two-factor authentication required for user {Email} ({UserId}).",
                    user.Email,
                    user.Id);

                return RedirectToPage(
                    "./LoginWith2fa",
                    new
                    {
                        ReturnUrl = returnUrl,
                        RememberMe = Input.RememberMe
                    });
            }

            // ====================================================
            // LOCKOUT
            // ====================================================

            if (result.IsLockedOut)
            {
                _logger.LogWarning(
                    "User {Email} ({UserId}) account is locked out.",
                    user.Email,
                    user.Id);

                return RedirectToPage(
                    "./Lockout");
            }

            // ====================================================
            // NOT ALLOWED
            // ====================================================

            if (result.IsNotAllowed)
            {
                _logger.LogWarning(
                    "User {Email} ({UserId}) is not allowed to sign in.",
                    user.Email,
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    "Your account is currently not allowed to sign in.");

                return Page();
            }

            // ====================================================
            // INVALID PASSWORD
            // ====================================================

            _logger.LogWarning(
                "Invalid password supplied for user {Email} ({UserId}). PasswordValid={PasswordValid}",
                user.Email,
                user.Id,
                passwordValid);

            ModelState.AddModelError(
                string.Empty,
                "Invalid login attempt.");

            return Page();
        }

        // ========================================================
        // LOCAL ADMIN CHECK
        // ========================================================

        private static bool IsLocalAdmin(
            ApplicationUser user)
        {
            return string.Equals(
                       user.Email,
                       LocalAdminEmail,
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   string.Equals(
                       user.UserName,
                       LocalAdminEmail,
                       StringComparison.OrdinalIgnoreCase);
        }

        // ========================================================
        // ENSURE LOCAL ADMIN ROLES
        // ========================================================

        private async Task EnsureLocalAdminRolesAsync(
            ApplicationUser user)
        {
            string[] requiredAdminRoles =
            {
                "Admin",
                "CTSHIPAdmin"
            };

            foreach (var role in requiredAdminRoles)
            {
                if (!await _roleManager.RoleExistsAsync(
                        role))
                {
                    var createRoleResult =
                        await _roleManager.CreateAsync(
                            new IdentityRole(role));

                    if (!createRoleResult.Succeeded)
                    {
                        _logger.LogError(
                            "Failed to create role {Role}. Errors: {Errors}",
                            role,
                            string.Join(
                                ", ",
                                createRoleResult.Errors
                                    .Select(
                                        e =>
                                            e.Description)));

                        continue;
                    }
                }

                if (!await _userManager.IsInRoleAsync(
                        user,
                        role))
                {
                    var addRoleResult =
                        await _userManager.AddToRoleAsync(
                            user,
                            role);

                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError(
                            "Failed to add user {UserId} to role {Role}. Errors: {Errors}",
                            user.Id,
                            role,
                            string.Join(
                                ", ",
                                addRoleResult.Errors
                                    .Select(
                                        e =>
                                            e.Description)));
                    }
                }
            }
        }
    }
}