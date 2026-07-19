// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace CTSHIPDashboard.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
            _context = context;
        }

        public string Username { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;

        [TempData]
        public string StatusMessageTemp { get; set; } = string.Empty;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Display(Name = "Full Name")]
            public string? FullName { get; set; }

            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            public string? PhotoPath { get; set; }
            public string? Role { get; set; }
            public string? HmoName { get; set; }
            public string? ProviderName { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            Username = user.UserName ?? string.Empty;

            var roles = await _userManager.GetRolesAsync(user);
            Input.Role = roles.FirstOrDefault();

            Input.FullName = user.FullName;
            Input.PhoneNumber = user.PhoneNumber;
            Input.PhotoPath = ProfilePhotoStorage.ResolvePhotoPath(user.Id, _env);

            // Optional: Load HMO/Provider name
            if (user.HmoId.HasValue)
            {
                var hmo = await _context.Hmos.FindAsync(user.HmoId);
                Input.HmoName = hmo?.Name;
            }

            if (user.ProviderId.HasValue)
            {
                var provider = await _context.Providers.FindAsync(user.ProviderId);
                Input.ProviderName = provider?.Name;
            }

            StatusMessage = StatusMessageTemp;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                StatusMessageTemp = "Error: Please fix the errors below.";
                return RedirectToPage();
            }

            // Update basic info
            if (user.FullName != Input.FullName)
                user.FullName = Input.FullName;

            if (user.PhoneNumber != Input.PhoneNumber)
                user.PhoneNumber = Input.PhoneNumber;

            if (photo != null)
            {
                try
                {
                    await ProfilePhotoStorage.SaveAsync(photo, user.Id, _env);
                }
                catch (InvalidOperationException exception)
                {
                    StatusMessageTemp = $"Error: {exception.Message}";
                    return RedirectToPage();
                }
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessageTemp = "Error: Your profile could not be updated.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);

            StatusMessageTemp = "Your profile has been updated successfully!";
            return RedirectToPage();
        }
    }
}
