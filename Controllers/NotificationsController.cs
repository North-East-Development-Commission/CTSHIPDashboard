using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        List<string> groups = await ResolveNotificationGroupsAsync(user, cancellationToken);
        List<NotificationListItem> notifications = await BuildNotificationQuery(groups, user.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        List<string> groups = await ResolveNotificationGroupsAsync(user, cancellationToken);
        List<NotificationListItem> notifications = await BuildNotificationQuery(groups, user.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Json(notifications.Select(x => new
        {
            x.Id,
            x.Title,
            x.Message,
            x.Url,
            x.Icon,
            x.CreatedAt,
            CreatedAtText = x.CreatedAt.ToLocalTime().ToString("dd MMM, h:mm tt"),
            x.IsUnread
        }));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { count = 0 });
        }

        List<string> groups = await ResolveNotificationGroupsAsync(user, cancellationToken);
        int count = await _context.AppNotifications
            .AsNoTracking()
            .Where(x => groups.Contains(x.TargetGroup))
            .Where(x => !_context.AppNotificationReads.Any(read =>
                read.AppNotificationId == x.Id && read.UserId == user.Id))
            .CountAsync(cancellationToken);

        return Json(new { count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        List<string> groups = await ResolveNotificationGroupsAsync(user, cancellationToken);
        bool canRead = await _context.AppNotifications
            .AnyAsync(x => x.Id == id && groups.Contains(x.TargetGroup), cancellationToken);
        if (!canRead)
        {
            return NotFound();
        }

        bool alreadyRead = await _context.AppNotificationReads
            .AnyAsync(x => x.AppNotificationId == id && x.UserId == user.Id, cancellationToken);
        if (!alreadyRead)
        {
            _context.AppNotificationReads.Add(new AppNotificationRead
            {
                AppNotificationId = id,
                UserId = user.Id,
                ReadAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Forbid();
        }

        List<string> groups = await ResolveNotificationGroupsAsync(user, cancellationToken);
        List<int> unreadIds = await _context.AppNotifications
            .AsNoTracking()
            .Where(x => groups.Contains(x.TargetGroup))
            .Where(x => !_context.AppNotificationReads.Any(read =>
                read.AppNotificationId == x.Id && read.UserId == user.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (int notificationId in unreadIds)
        {
            _context.AppNotificationReads.Add(new AppNotificationRead
            {
                AppNotificationId = notificationId,
                UserId = user.Id,
                ReadAt = DateTime.UtcNow
            });
        }

        if (unreadIds.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, count = unreadIds.Count });
    }

    private IQueryable<NotificationListItem> BuildNotificationQuery(
        List<string> groups,
        string userId)
    {
        return _context.AppNotifications
            .AsNoTracking()
            .Where(x => groups.Contains(x.TargetGroup))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationListItem
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Url = x.Url,
                Icon = x.Icon,
                CreatedAt = x.CreatedAt,
                IsUnread = !_context.AppNotificationReads.Any(read =>
                    read.AppNotificationId == x.Id && read.UserId == userId)
            });
    }

    private async Task<List<string>> ResolveNotificationGroupsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        List<string> groups = new() { NotificationGroups.User(user.Id) };

        IList<string> roles = await _userManager.GetRolesAsync(user);
        groups.AddRange(roles.Select(NotificationGroups.Role));

        if (user.HmoId.HasValue)
        {
            groups.Add(NotificationGroups.Hmo(user.HmoId.Value));
            string? hmoCode = await _context.Hmos
                .AsNoTracking()
                .Where(x => x.Id == user.HmoId.Value)
                .Select(x => x.RegistrationNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(hmoCode))
            {
                groups.Add(NotificationGroups.HmoCode(hmoCode));
            }
        }

        if (user.ProviderId.HasValue)
        {
            groups.Add(NotificationGroups.Provider(user.ProviderId.Value));
        }

        if (roles.Any(role => string.Equals(role, "ReferralPro", StringComparison.OrdinalIgnoreCase)))
        {
            Guid? hospitalId = await GetReferralHospitalIdAsync(user, cancellationToken);
            if (hospitalId.HasValue)
            {
                groups.Add(NotificationGroups.ReferralHospital(hospitalId.Value));
            }
        }

        return groups.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<Guid?> GetReferralHospitalIdAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!user.ProviderId.HasValue)
        {
            return null;
        }

        var provider = await _context.Providers
            .AsNoTracking()
            .Where(x => x.Id == user.ProviderId.Value)
            .Select(x => new { x.Name, x.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (provider == null)
        {
            return null;
        }

        string providerName = provider.Name.Trim().ToUpperInvariant();
        string? providerEmail = string.IsNullOrWhiteSpace(provider.Email)
            ? null
            : provider.Email.Trim().ToUpperInvariant();

        return await _context.ReferralHospitals
            .AsNoTracking()
            .Where(x => x.IsActive &&
                (x.Name.ToUpper() == providerName ||
                 (providerEmail != null && x.Email != null && x.Email.ToUpper() == providerEmail)))
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public class NotificationListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Icon { get; set; } = "info";
        public DateTime CreatedAt { get; set; }
        public bool IsUnread { get; set; }
    }
}
