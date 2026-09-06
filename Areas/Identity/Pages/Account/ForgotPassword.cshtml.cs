#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace CTSHIPDashboard.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordResetEmailSender _passwordResetEmailSender;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IPasswordResetEmailSender passwordResetEmailSender,
            IWebHostEnvironment environment,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _passwordResetEmailSender = passwordResetEmailSender;
            _environment = environment;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            string code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            string callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                _logger.LogError("Password reset callback URL could not be generated for {Email}.", Input.Email);
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            bool sent = await _passwordResetEmailSender.SendPasswordResetAsync(
                user.Email ?? Input.Email.Trim(),
                callbackUrl,
                HttpContext.RequestAborted);

            if (!sent && _environment.IsDevelopment())
            {
                TempData["PasswordResetLink"] = callbackUrl;
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
