using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services
{
    public static class ApplicationStartupInitializer
    {
        private static readonly string[] RequiredRoles =
        {
            "Admin",
            "CTSHIPAdmin",
            "HMO",
            "HmoEnrollmentOfficer",
            "Provider",
            "SSHIA",
            "Monitoring",
            "Auditor",
            "Finance",
            "Reviewer",
            "StateOffice",
            "NHIA",
            "NEDCAdmin"
        };

        public static async Task InitializeAsync(
            IServiceProvider serviceProvider,
            ILogger logger)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await EnsureRequiredRolesAsync(roleManager);
                await NormalizeMonitoringTargetScopesAsync(context);

                logger.LogInformation("CTSHIP startup maintenance completed.");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "CTSHIP startup maintenance failed. The web host will continue running.");
            }
        }

        private static async Task EnsureRequiredRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (string role in RequiredRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task NormalizeMonitoringTargetScopesAsync(ApplicationDbContext context)
        {
            ProgramMonitoringTarget? legacyTarget = await context.ProgramMonitoringTargets
                .FirstOrDefaultAsync(target => target.Scope == "North East");
            if (legacyTarget == null)
            {
                return;
            }

            ProgramMonitoringTarget? ctsTarget = await context.ProgramMonitoringTargets
                .FirstOrDefaultAsync(target => target.Scope == "CTSHIP");

            if (ctsTarget == null)
            {
                legacyTarget.Scope = "CTSHIP";
            }
            else
            {
                if (ctsTarget.TargetEnrollees <= 0)
                {
                    ctsTarget.TargetEnrollees = legacyTarget.TargetEnrollees;
                }

                context.ProgramMonitoringTargets.Remove(legacyTarget);
            }

            await context.SaveChangesAsync();
        }
    }
}
