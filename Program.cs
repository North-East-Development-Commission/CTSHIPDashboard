using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var requireHttps = builder.Configuration.GetValue("Security:RequireHttps", true);
var cookieSecurePolicy = requireHttps
    ? CookieSecurePolicy.Always
    : CookieSecurePolicy.SameAsRequest;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
var missingConnectionString =
    "Server=localhost;Database=MissingDefaultConnection;User Id=missing;Password=missing;Encrypt=True;TrustServerCertificate=True;Connection Timeout=1;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        hasConnectionString ? connectionString : missingConnectionString,
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
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = cookieSecurePolicy;
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

// Configure ForwardedHeaders for Azure and proxies
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

if (isAzureAppService)
{
    // Azure App Service: Allow all known proxies
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}

app.UseForwardedHeaders(forwardedHeadersOptions);

OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("CTSHIP NEDC Project");

if (!hasConnectionString)
{
    app.Logger.LogError("Connection string 'DefaultConnection' was not found. The web host will start, but database readiness will fail.");
}
else
{
    try
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            app.Logger.LogInformation("Applying pending database migrations.");
            await context.Database.MigrateAsync();
            app.Logger.LogInformation("Database migrations completed.");
        }

        // CREATE ROLES
        string[] requiredRoles = { "Admin", "CTSHIPAdmin", "HmoEnrollmentOfficer", "IHSA", "HMO", "Provider", "StateOffice", "NHIA" };
        foreach (var roleName in requiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                app.Logger.LogInformation($"Role created: {roleName}");
            }
        }

        // CREATE ADMIN USER
        string adminEmail = "as.maiwada@nedc.gov.ng";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "CTSHIP Admin",
                State = "Borno"
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@2025");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                await userManager.AddToRoleAsync(adminUser, "CTSHIPAdmin");
                app.Logger.LogInformation($"Admin user created: {adminEmail} | Password: Admin@2025");
            }
            else
            {
                app.Logger.LogError($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // Ensure ProgramMonitoringTargets data
        var legacyTarget = context.ProgramMonitoringTargets.FirstOrDefault(t => t.Scope == "North East");
        var ctsTarget = context.ProgramMonitoringTargets.FirstOrDefault(t => t.Scope == "CTSHIP");

        if (legacyTarget != null && ctsTarget == null)
        {
            legacyTarget.Scope = "CTSHIP";
            context.SaveChanges();
        }

        app.Logger.LogInformation("Startup initialization completed successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError($"Startup initialization error: {ex.Message}");
    }
}

app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

if (requireHttps && !app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (requireHttps)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/", context =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        string dashboardPath =
            context.User.IsInRole("Admin") ? "/Analytics/Index"
            : context.User.IsInRole("HmoEnrollmentOfficer") ? "/Enrollees/Dashboard"
            : context.User.IsInRole("HMO") ? "/Hmo/Dashboard"
            : context.User.IsInRole("ReferralPro") ? "/ReferralPro/Dashboard"
            : context.User.IsInRole("Provider") ? "/Providers/Dashboard"
            : context.User.IsInRole("Finance") ? "/Finance/Dashboard"
            : context.User.IsInRole("StateOffice") ? "/StateOffice/Index"
            : context.User.IsInRole("NHIA") ? "/NHIA/Dashboard"
            : context.User.IsInRole("SSHIA") ? "/SSHIA/Dashboard"
            : context.User.IsInRole("IHSA") || context.User.IsInRole("NEDCAdmin") ? "/IHSA/Dashboard"
            : context.User.IsInRole("Monitoring") ? "/Monitoring/Index"
            : "/Home/Index";
        context.Response.Redirect(dashboardPath);
        return Task.CompletedTask;
    }
    context.Response.Redirect("/Identity/Account/Login");
    return Task.CompletedTask;
});

app.MapControllerRoute(name: "default", pattern: "{controller=Analytics}/{action=Index}/{id?}");
app.MapHub<AnalyticsHub>("/analyticsHub");
app.MapGet("/alive", () => Results.Ok(new { status = "alive", application = "CTSHIPDashboard", version = "2026-08-15-01" })).AllowAnonymous();
app.MapGet("/health", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (!hasConnectionString)
    {
        return Results.Json(new { status = "unhealthy", reason = "missing_connection_string" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    try
    {
        using var scope = services.CreateScope();
        var healthContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        if (!await healthContext.Database.CanConnectAsync(timeout.Token))
        {
            return Results.Json(new { status = "unhealthy", reason = "database_unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        var pendingMigrations = await healthContext.Database.GetPendingMigrationsAsync(timeout.Token);
        return pendingMigrations.Any() ? Results.Json(new { status = "unhealthy", reason = "pending_migrations", pendingMigrations = pendingMigrations.ToArray() }, statusCode: StatusCodes.Status503ServiceUnavailable) : Results.Ok(new { status = "healthy" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", reason = "database_probe_failed", error = ex.GetType().Name }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapRazorPages().WithStaticAssets();
app.Run();
