// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace CTSHIPDashboard.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var now = DateTime.Now;
                var actor = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.Email ?? user.UserName ?? user.Id
                    : $"{user.FullName} ({user.Email ?? user.UserName})";

                _context.UserActivities.Add(new UserActivity
                {
                    UserId = user.Id,
                    Action = "Logout",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    DeviceInfo = Request.Headers.UserAgent.ToString(),
                    Timestamp = now
                });
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "Account.Logout",
                    PerformedBy = actor,
                    Details = $"User logged out; IP {HttpContext.Connection.RemoteIpAddress}",
                    Timestamp = now
                });
                await _context.SaveChangesAsync();
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
