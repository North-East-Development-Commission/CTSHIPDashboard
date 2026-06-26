// Middleware/UserActivityMiddleware.cs
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
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

            await _next(context);

            if (user == null || context.Response.StatusCode >= 400)
            {
                return;
            }

            ControllerActionDescriptor? descriptor =
                context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();

            if (descriptor == null)
            {
                return;
            }

            var now = DateTime.Now;
            var actor = string.IsNullOrWhiteSpace(user.FullName)
                ? user.Email ?? user.UserName ?? user.Id
                : $"{user.FullName} ({user.Email ?? user.UserName})";

            var lastActivityAt = await dbContext.UserActivities
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => (DateTime?)a.Timestamp)
                .FirstOrDefaultAsync();

            if (!lastActivityAt.HasValue || (now - lastActivityAt.Value).TotalMinutes > 2)
            {
                dbContext.UserActivities.Add(new UserActivity
                {
                    UserId = user.Id,
                    Action = "Active",
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    DeviceInfo = context.Request.Headers.UserAgent.ToString(),
                    Timestamp = now
                });
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = $"{descriptor.ControllerName}.{descriptor.ActionName}",
                PerformedBy = actor,
                Details = $"{context.Request.Method} {context.Request.Path}; Status {context.Response.StatusCode}; IP {context.Connection.RemoteIpAddress}",
                Timestamp = now
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
