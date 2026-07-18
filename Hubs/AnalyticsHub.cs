using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Hubs
{
    public class AnalyticsHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnalyticsHub(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                ApplicationUser? user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        NotificationGroups.User(user.Id),
                        Context.ConnectionAborted);

                    IList<string> roles = await _userManager.GetRolesAsync(user);
                    foreach (string role in roles)
                    {
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId,
                            NotificationGroups.Role(role),
                            Context.ConnectionAborted);
                    }

                    if (user.HmoId.HasValue)
                    {
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId,
                            NotificationGroups.Hmo(user.HmoId.Value),
                            Context.ConnectionAborted);

                        string? hmoCode = await _context.Hmos
                            .AsNoTracking()
                            .Where(x => x.Id == user.HmoId.Value)
                            .Select(x => x.RegistrationNumber)
                            .FirstOrDefaultAsync(Context.ConnectionAborted);

                        if (!string.IsNullOrWhiteSpace(hmoCode))
                        {
                            await Groups.AddToGroupAsync(
                                Context.ConnectionId,
                                NotificationGroups.HmoCode(hmoCode),
                                Context.ConnectionAborted);
                        }
                    }

                    if (user.ProviderId.HasValue)
                    {
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId,
                            NotificationGroups.Provider(user.ProviderId.Value),
                            Context.ConnectionAborted);
                    }

                    if (roles.Any(role =>
                        string.Equals(role, "ReferralPro", StringComparison.OrdinalIgnoreCase)))
                    {
                        Guid? referralHospitalId =
                            await GetReferralHospitalIdAsync(user, Context.ConnectionAborted);

                        if (referralHospitalId.HasValue)
                        {
                            await Groups.AddToGroupAsync(
                                Context.ConnectionId,
                                NotificationGroups.ReferralHospital(referralHospitalId.Value),
                                Context.ConnectionAborted);
                        }
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        public async Task SendUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveUpdate", message);
        }

        private async Task<Guid?> GetReferralHospitalIdAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            if (user.ProviderId.HasValue)
            {
                var provider = await _context.Providers
                    .AsNoTracking()
                    .Where(x => x.Id == user.ProviderId.Value)
                    .Select(x => new { x.Name, x.Email })
                    .FirstOrDefaultAsync(cancellationToken);

                if (provider != null)
                {
                    string providerName = provider.Name.Trim().ToUpperInvariant();
                    string? providerEmail = Normalize(provider.Email);

                    Guid? providerHospitalId = await _context.ReferralHospitals
                        .AsNoTracking()
                        .Where(x =>
                            x.IsActive &&
                            (x.Name.ToUpper() == providerName ||
                             (providerEmail != null &&
                              x.Email != null &&
                              x.Email.ToUpper() == providerEmail)))
                        .Select(x => (Guid?)x.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (providerHospitalId.HasValue)
                    {
                        return providerHospitalId;
                    }
                }
            }

            string? email = Normalize(user.Email ?? user.UserName);
            if (email == null)
            {
                return null;
            }

            return await _context.ReferralHospitals
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Email != null &&
                    x.Email.ToUpper() == email)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().ToUpperInvariant();
        }
    }
}
