using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Provider,ReferralPro,CTSHIPAdmin")]
    public class ReferralPrintController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReferralPrintController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> PrintSlip(Guid id)
        {
            Referral? referral = await _context.Referrals
                .AsNoTracking()
                .Include(r => r.ReferredHospital)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (referral == null) return NotFound();

            // Only allow printing after HMO approval (code issued)
            if (string.IsNullOrWhiteSpace(referral.ReferralVerificationCode) || !referral.ReferralVerificationCodeIssuedAt.HasValue)
            {
                TempData["Error"] = "Referral code not yet issued. The referral must be approved by the HMO before printing the slip.";
                return RedirectToAction("Details", "ReferralPro", new { id });
            }

            // Providers may only print slips for referrals they created
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("Provider") && currentUser?.ProviderId.HasValue == true)
            {
                string providerIdStr = currentUser.ProviderId.Value.ToString();
                if (referral.FromProviderId != providerIdStr && !User.IsInRole("CTSHIPAdmin"))
                {
                    return Forbid();
                }
            }

            return View(referral);
        }
    }
}
