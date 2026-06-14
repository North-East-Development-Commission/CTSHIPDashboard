// Middleware/UserActivityMiddleware.cs
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Middleware
{
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;

        public UserActivityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            var user = await userManager.GetUserAsync(context.User);
            var path = context.Request.Path.Value?.ToLower();

            // Only log authenticated users on actual pages (not static files, hubs, etc.)
            if (user != null && context.Response.StatusCode < 400 && path != null && !path.Contains("/favicon.ico"))
            {
                // Avoid logging too frequently (max once every 2 minutes per user)
                var lastActivity = await dbContext.UserActivities
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefaultAsync();

                if (lastActivity == null || (DateTime.Now - lastActivity.Timestamp).TotalMinutes > 2)
                {
                    var activity = new UserActivity
                    {
                        UserId = user.Id,
                        Action = "Active",
                        IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                        DeviceInfo = context.Request.Headers["User-Agent"].ToString(),
                        Timestamp = DateTime.Now
                    };

                    dbContext.UserActivities.Add(activity);
                    await dbContext.SaveChangesAsync();
                }
            }

            await _next(context);
        }
    }
}