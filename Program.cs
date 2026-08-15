using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Use providers that work consistently in IIS, local development, and restricted hosts.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlServerOptions.CommandTimeout(60);
        }));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Error/403";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;
});

var azureHomePath = Environment.GetEnvironmentVariable("HOME");
var isAzureAppService = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
var dataProtectionKeysPath = isAzureAppService && !string.IsNullOrWhiteSpace(azureHomePath)
    ? Path.Combine(azureHomePath, "DataProtectionKeys")
    : Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("CTSHIPDashboard");

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddScoped<IClaimsTransformation, CtshipAdminClaimsTransformation>();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("CTSHIPAdmin", "Admin")));
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IAppNotificationService, AppNotificationService>();
builder.Services.AddScoped<IDeathRegisterService, DeathRegisterService>();
builder.Services.AddScoped<IMonitoringIndicatorService, MonitoringIndicatorService>();
builder.Services.AddScoped<IAuditService, AuditService>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("CTSHIP NEDC Project");

try
{
    using var scope = app.Services.CreateScope();
    ApplicationDbContext context =
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    RoleManager<IdentityRole> roleManager =
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    IServiceProvider services = scope.ServiceProvider;

    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        app.Logger.LogInformation("Applying pending database migrations.");
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations completed.");
    }

    foreach (string requiredRole in new[] { "HmoEnrollmentOfficer", "IHSA" })
    {
        if (!await roleManager.RoleExistsAsync(requiredRole))
        {
            await roleManager.CreateAsync(new IdentityRole(requiredRole));
        }
    }

    var legacyTarget = context.ProgramMonitoringTargets
        .FirstOrDefault(target => target.Scope == "North East");
    var ctsTarget = context.ProgramMonitoringTargets
        .FirstOrDefault(target => target.Scope == "CTSHIP");

    if (legacyTarget != null)
    {
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

        context.SaveChanges();
    }

    app.Logger.LogInformation("CTSHIP startup data initialization completed.");
}
catch (Exception exception)
{
    // Startup data problems must not terminate the web host. Requests that
    // depend on the database will be handled by the friendly error boundary.
    try
    {
        app.Logger.LogError(
            exception,
            "Startup data initialization failed. The web host will continue running.");
    }
    catch
    {
        Console.Error.WriteLine(
            $"Startup data initialization failed: {exception.Message}");
    }
}

// Friendly handling is enabled in every environment so users never receive
// raw framework exception pages.
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
//app.UseMiddleware<CTSHIPDashboard.Middleware.UserActivityMiddleware>();

app.MapStaticAssets();

app.MapGet("/", context =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        string dashboardPath =
            context.User.IsInRole("Admin")
                ? "/Analytics/Index"
                : context.User.IsInRole("HmoEnrollmentOfficer")
                    ? "/Enrollees/Dashboard"
                : context.User.IsInRole("HMO")
                    ? "/Hmo/Dashboard"
                    : context.User.IsInRole("ReferralPro")
                        ? "/ReferralPro/Dashboard"
                    : context.User.IsInRole("Provider")
                        ? "/Providers/Dashboard"
                        : context.User.IsInRole("Finance")
                            ? "/Finance/Dashboard"
                            : context.User.IsInRole("StateOffice")
                                ? "/StateOffice/Index"
                                : context.User.IsInRole("NHIA")
                                    ? "/NHIA/Dashboard"
                                    : context.User.IsInRole("SSHIA")
                                        ? "/SSHIA/Dashboard"
                                        : context.User.IsInRole("IHSA") || context.User.IsInRole("NEDCAdmin")
                                            ? "/IHSA/Dashboard"
                                            : context.User.IsInRole("Monitoring")
                                                ? "/Monitoring/Index"
                                                : "/Home/Index";

        context.Response.Redirect(dashboardPath);
        return Task.CompletedTask;
    }

    context.Response.Redirect("/Identity/Account/Login");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Analytics}/{action=Index}/{id?}");

app.MapHub<AnalyticsHub>("/analyticsHub");
app.MapGet("/health", async (
    ApplicationDbContext context,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var pendingMigrations = await context.Database
            .GetPendingMigrationsAsync(cancellationToken);

        return pendingMigrations.Any()
            ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new { status = "healthy" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
//#FE9031 
//#FE9031



