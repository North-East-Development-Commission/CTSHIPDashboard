// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Data;

namespace CTSHIPDashboard.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

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

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // 1. Check if user is active BEFORE attempting sign in
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user != null && user.GetType().GetProperty("IsActive") != null)
                {
                    bool isActive = (bool?)user.GetType().GetProperty("IsActive")?.GetValue(user, null) ?? true;
                    if (!isActive)
                    {
                        ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact support.");
                        return Page();
                    }
                }

                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (user != null)
                    {
                        var now = DateTime.Now;
                        var actor = string.IsNullOrWhiteSpace(user.FullName)
                            ? user.Email ?? user.UserName ?? user.Id
                            : $"{user.FullName} ({user.Email ?? user.UserName})";

                        _context.UserActivities.Add(new UserActivity
                        {
                            UserId = user.Id,
                            Action = "Login",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers.UserAgent.ToString(),
                            Timestamp = now
                        });
                        _context.AuditLogs.Add(new AuditLog
                        {
                            Action = "Account.Login",
                            PerformedBy = actor,
                            Details = $"Successful login; IP {HttpContext.Connection.RemoteIpAddress}",
                            Timestamp = now
                        });
                        await _context.SaveChangesAsync();

                        var roles = await _userManager.GetRolesAsync(user);

                        // 2. Explicit and consistent clean path routing fallback mechanics
                        if (roles.Contains("Admin"))
                            return RedirectToAction("Index", "Analytics", new { area = "" });

                        else if (roles.Contains("NEDCAdmin") || roles.Contains("SSHIA"))
                            return RedirectToAction("Index", "Analytics", new { area = "" });

                        else if (roles.Contains("HMO"))
                            return RedirectToAction("Dashboard", "HMO", new { area = "" });

                        else if (roles.Contains("Provider"))
                            return RedirectToAction("Dashboard", "Providers", new { area = "" });

                        else if (roles.Contains("Finance"))
                            return RedirectToAction("Dashboard", "Finance", new { area = "" });

                        else if (roles.Contains("StateOffice"))
                            return RedirectToAction("Index", "StateOffice", new { area = "" });

                        else if (roles.Contains("NHIA"))
                            return RedirectToAction("Dashboard", "NHIA", new { area = "" });

                        else if (roles.Contains("Monitoring"))
                            return RedirectToAction("Index", "Monitoring", new { area = "" });

                        else
                            return LocalRedirect(returnUrl);
                    }
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            return Page();
        }
    }
}
