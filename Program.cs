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

// ============================================================
// SECURITY / HTTPS CONFIGURATION
// ============================================================

var requireHttps =
    builder.Configuration.GetValue("Security:RequireHttps", true);

var cookieSecurePolicy =
    requireHttps
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;


// ============================================================
// LOGGING
// ============================================================

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();


// ============================================================
// DATABASE CONNECTION
// ============================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

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


// ============================================================
// ASP.NET CORE IDENTITY
// ============================================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Allow users to log in without account/email confirmation.
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        // Lockout protection.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();


// ============================================================
// APPLICATION COOKIE
// ============================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Error/403";

    options.Cookie.Name = ".CTSHIPDashboard.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;

    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});


// ============================================================
// ANTIFORGERY
// ============================================================

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});


// ============================================================
// COOKIE POLICY
// ============================================================

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = cookieSecurePolicy;
});


// ============================================================
// DATA PROTECTION
// ============================================================

var azureHomePath =
    Environment.GetEnvironmentVariable("HOME");

var isAzureAppService =
    !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

var dataProtectionKeysPath =
    isAzureAppService &&
    !string.IsNullOrWhiteSpace(azureHomePath)
        ? Path.Combine(
            azureHomePath,
            "DataProtectionKeys")
        : Path.Combine(
            builder.Environment.ContentRootPath,
            "App_Data",
            "DataProtectionKeys");

Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("CTSHIPDashboard");


// ============================================================
// MVC / RAZOR PAGES / SIGNALR
// ============================================================

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();


// ============================================================
// CLAIMS TRANSFORMATION
// ============================================================

builder.Services.AddScoped<
    IClaimsTransformation,
    CtshipAdminClaimsTransformation>();


// ============================================================
// AUTHORIZATION
// ============================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AdminOnly",
        policy =>
            policy.RequireRole(
                "CTSHIPAdmin",
                "Admin"));
});


// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<
    IReferralService,
    ReferralService>();

builder.Services.AddScoped<
    IAppNotificationService,
    AppNotificationService>();

builder.Services.AddScoped<
    IDeathRegisterService,
    DeathRegisterService>();

builder.Services.AddScoped<
    IMonitoringIndicatorService,
    MonitoringIndicatorService>();

builder.Services.AddScoped<
    IAuditService,
    AuditService>();


// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();


// ============================================================
// FORWARDED HEADERS - AZURE / PROXY
// ============================================================

var forwardedHeadersOptions =
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    };

if (isAzureAppService)
{
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}

app.UseForwardedHeaders(forwardedHeadersOptions);


// ============================================================
// EPPLUS
// ============================================================

OfficeOpenXml.ExcelPackage.License
    .SetNonCommercialPersonal(
        "CTSHIP NEDC Project");


// ============================================================
// DATABASE / IDENTITY STARTUP INITIALIZATION
// ============================================================

try
{
    using var scope =
        app.Services.CreateScope();

    var context =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    var roleManager =
        scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();


    // ============================================================
    // APPLY DATABASE MIGRATIONS
    // ============================================================

    if (builder.Configuration
        .GetValue<bool>(
            "Database:ApplyMigrationsOnStartup"))
    {
        app.Logger.LogInformation(
            "Applying pending database migrations.");

        await context.Database.MigrateAsync();

        app.Logger.LogInformation(
            "Database migrations completed.");
    }


    // ============================================================
    // CREATE REQUIRED APPLICATION ROLES
    // ============================================================

    string[] requiredRoles =
    {
        "Admin",
        "CTSHIPAdmin",
        "HmoEnrollmentOfficer",
        "IHSA",
        "HMO",
        "Provider",
        "ReferralPro",
        "Finance",
        "StateOffice",
        "NHIA",
        "SSHIA",
        "NEDCAdmin",
        "Monitoring"
    };

    foreach (var roleName in requiredRoles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var createRoleResult =
                await roleManager.CreateAsync(
                    new IdentityRole(roleName));

            if (createRoleResult.Succeeded)
            {
                app.Logger.LogInformation(
                    "Role created: {RoleName}",
                    roleName);
            }
            else
            {
                app.Logger.LogError(
                    "Failed to create role {RoleName}. Errors: {Errors}",
                    roleName,
                    string.Join(
                        ", ",
                        createRoleResult.Errors
                            .Select(e => e.Description)));
            }
        }
    }


    // ============================================================
    // CREATE ADMIN USER
    // ============================================================

    const string adminEmail =
        "as.maiwada@nedc.gov.ng";

    const string adminPassword =
        "Admin@2025";

    var adminUser =
        await userManager.FindByEmailAsync(
            adminEmail);

    if (adminUser == null)
    {
        adminUser =
            new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "CTSHIP Admin",
                State = "Borno"
            };

        var createAdminResult =
            await userManager.CreateAsync(
                adminUser,
                adminPassword);

        if (createAdminResult.Succeeded)
        {
            app.Logger.LogInformation(
                "Admin user created successfully: {AdminEmail}",
                adminEmail);
        }
        else
        {
            app.Logger.LogError(
                "Failed to create admin user {AdminEmail}. Errors: {Errors}",
                adminEmail,
                string.Join(
                    ", ",
                    createAdminResult.Errors
                        .Select(e => e.Description)));

            adminUser = null;
        }
    }


    // ============================================================
    // REPAIR / VERIFY EXISTING ADMIN USER
    // ============================================================

    if (adminUser != null)
    {
        var adminChanged = false;

        if (string.IsNullOrWhiteSpace(
                adminUser.UserName))
        {
            adminUser.UserName =
                adminEmail;

            adminChanged = true;
        }

        if (string.IsNullOrWhiteSpace(
                adminUser.Email))
        {
            adminUser.Email =
                adminEmail;

            adminChanged = true;
        }

        if (!adminUser.EmailConfirmed)
        {
            adminUser.EmailConfirmed =
                true;

            adminChanged = true;
        }

        if (adminChanged)
        {
            var updateAdminResult =
                await userManager.UpdateAsync(
                    adminUser);

            if (!updateAdminResult.Succeeded)
            {
                app.Logger.LogError(
                    "Failed to update admin user {AdminEmail}. Errors: {Errors}",
                    adminEmail,
                    string.Join(
                        ", ",
                        updateAdminResult.Errors
                            .Select(e => e.Description)));
            }
        }


        // ============================================================
        // ENSURE ADMIN ROLE
        // ============================================================

        if (!await userManager.IsInRoleAsync(
                adminUser,
                "Admin"))
        {
            var addAdminRoleResult =
                await userManager.AddToRoleAsync(
                    adminUser,
                    "Admin");

            if (addAdminRoleResult.Succeeded)
            {
                app.Logger.LogInformation(
                    "Admin role assigned to {AdminEmail}.",
                    adminEmail);
            }
            else
            {
                app.Logger.LogError(
                    "Failed to assign Admin role to {AdminEmail}. Errors: {Errors}",
                    adminEmail,
                    string.Join(
                        ", ",
                        addAdminRoleResult.Errors
                            .Select(e => e.Description)));
            }
        }


        // ============================================================
        // ENSURE CTSHIP ADMIN ROLE
        // ============================================================

        if (!await userManager.IsInRoleAsync(
                adminUser,
                "CTSHIPAdmin"))
        {
            var addCtshipAdminRoleResult =
                await userManager.AddToRoleAsync(
                    adminUser,
                    "CTSHIPAdmin");

            if (addCtshipAdminRoleResult.Succeeded)
            {
                app.Logger.LogInformation(
                    "CTSHIPAdmin role assigned to {AdminEmail}.",
                    adminEmail);
            }
            else
            {
                app.Logger.LogError(
                    "Failed to assign CTSHIPAdmin role to {AdminEmail}. Errors: {Errors}",
                    adminEmail,
                    string.Join(
                        ", ",
                        addCtshipAdminRoleResult.Errors
                            .Select(e => e.Description)));
            }
        }
    }


    // ============================================================
    // ENSURE PROGRAM MONITORING TARGET DATA
    // ============================================================

    var legacyTarget =
        await context
            .ProgramMonitoringTargets
            .FirstOrDefaultAsync(
                t =>
                    t.Scope ==
                    "North East");

    var ctsTarget =
        await context
            .ProgramMonitoringTargets
            .FirstOrDefaultAsync(
                t =>
                    t.Scope ==
                    "CTSHIP");

    if (legacyTarget != null &&
        ctsTarget == null)
    {
        legacyTarget.Scope =
            "CTSHIP";

        await context.SaveChangesAsync();

        app.Logger.LogInformation(
            "ProgramMonitoringTarget scope updated from North East to CTSHIP.");
    }


    app.Logger.LogInformation(
        "Startup initialization completed successfully.");
}
catch (Exception ex)
{
    app.Logger.LogError(
        ex,
        "Startup initialization error.");
}


// ============================================================
// GLOBAL ERROR HANDLING
// ============================================================

app.UseExceptionHandler("/Error");

app.UseStatusCodePagesWithReExecute(
    "/Error/{0}");


// ============================================================
// HSTS
// ============================================================

if (requireHttps &&
    !app.Environment.IsDevelopment())
{
    app.UseHsts();
}


// ============================================================
// HTTPS
// ============================================================

if (requireHttps)
{
    app.UseHttpsRedirection();
}


// ============================================================
// STATIC FILES
// ============================================================

app.UseStaticFiles();


// ============================================================
// ROUTING
// ============================================================

app.UseRouting();


// ============================================================
// COOKIE POLICY
// ============================================================

app.UseCookiePolicy();


// ============================================================
// AUTHENTICATION / AUTHORIZATION
// ============================================================

app.UseAuthentication();
app.UseAuthorization();


// ============================================================
// STATIC ASSETS
// ============================================================

app.MapStaticAssets();


// ============================================================
// ROOT / ROLE-BASED DASHBOARD REDIRECTION
// ============================================================

app.MapGet("/", context =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        string dashboardPath;

        if (context.User.IsInRole("Admin") ||
            context.User.IsInRole("CTSHIPAdmin"))
        {
            dashboardPath =
                "/Analytics/Index";
        }
        else if (
            context.User.IsInRole(
                "HmoEnrollmentOfficer"))
        {
            dashboardPath =
                "/Enrollees/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "HMO"))
        {
            dashboardPath =
                "/Hmo/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "ReferralPro"))
        {
            dashboardPath =
                "/ReferralPro/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "Provider"))
        {
            dashboardPath =
                "/Providers/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "Finance"))
        {
            dashboardPath =
                "/Finance/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "StateOffice"))
        {
            dashboardPath =
                "/StateOffice/Index";
        }
        else if (
            context.User.IsInRole(
                "NHIA"))
        {
            dashboardPath =
                "/NHIA/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "SSHIA"))
        {
            dashboardPath =
                "/SSHIA/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "NEDCAdmin"))
        {
            dashboardPath =
            "/Analytics/Index";
        }
        else if (
            context.User.IsInRole(
                "IHSA"))
        {
            dashboardPath =
                "/IHSA/Dashboard";
        }
        else if (
            context.User.IsInRole(
                "Monitoring"))
        {
            dashboardPath =
                "/Monitoring/Index";
        }
        else
        {
            dashboardPath =
                "/Home/Index";
        }

        context.Response.Redirect(
            dashboardPath);

        return Task.CompletedTask;
    }

    context.Response.Redirect(
        "/Identity/Account/Login");

    return Task.CompletedTask;
});


// ============================================================
// MVC ROUTES
// ============================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Analytics}/{action=Index}/{id?}");


// ============================================================
// SIGNALR
// ============================================================

app.MapHub<AnalyticsHub>(
    "/analyticsHub");


// ============================================================
// ALIVE CHECK
// ============================================================

app.MapGet(
        "/alive",
        () =>
            Results.Ok(
                new
                {
                    status = "alive",
                    application =
                        "CTSHIPDashboard",
                    version =
                        "2026-08-19-01"
                }))
    .AllowAnonymous();


// ============================================================
// DATABASE HEALTH CHECK
// ============================================================

app.MapGet(
        "/health",
        async (
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            try
            {
                using var scope =
                    services.CreateScope();

                var healthContext =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ApplicationDbContext>();

                using var timeout =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(10));

                var canConnect =
                    await healthContext.Database
                        .CanConnectAsync(
                            timeout.Token);

                if (!canConnect)
                {
                    return Results.Json(
                        new
                        {
                            status =
                                "unhealthy",
                            reason =
                                "database_unreachable"
                        },
                        statusCode:
                            StatusCodes
                                .Status503ServiceUnavailable);
                }

                var pendingMigrations =
                    await healthContext.Database
                        .GetPendingMigrationsAsync(
                            timeout.Token);

                if (pendingMigrations.Any())
                {
                    return Results.Json(
                        new
                        {
                            status =
                                "unhealthy",
                            reason =
                                "pending_migrations",
                            pendingMigrations =
                                pendingMigrations.ToArray()
                        },
                        statusCode:
                            StatusCodes
                                .Status503ServiceUnavailable);
                }

                return Results.Ok(
                    new
                    {
                        status =
                            "healthy"
                    });
            }
            catch (OperationCanceledException)
            {
                return Results.Json(
                    new
                    {
                        status =
                            "unhealthy",
                        reason =
                            "database_probe_timeout"
                    },
                    statusCode:
                        StatusCodes
                            .Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(
                    ex,
                    "Database health probe failed.");

                return Results.Json(
                    new
                    {
                        status =
                            "unhealthy",
                        reason =
                            "database_probe_failed",
                        error =
                            ex.GetType().Name
                    },
                    statusCode:
                        StatusCodes
                            .Status503ServiceUnavailable);
            }
        })
    .AllowAnonymous();


// ============================================================
// RAZOR PAGES
// ============================================================

app.MapRazorPages()
    .WithStaticAssets();


// ============================================================
// START APPLICATION
// ============================================================

app.Run();