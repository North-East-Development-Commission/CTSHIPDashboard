using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
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
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Error/403";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IDeathRegisterService, DeathRegisterService>();
builder.Services.AddScoped<IMonitoringIndicatorService, MonitoringIndicatorService>();
builder.Services.AddScoped<IAuditService, AuditService>();

var app = builder.Build();

OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("CTSHIP NEDC Project");

try
{
    using var scope = app.Services.CreateScope();
    ApplicationDbContext context =
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    IServiceProvider services = scope.ServiceProvider;

    SeedData.SeedHmos(context);
    SeedData.SeedProviders(context);
    SeedData.SeedDoctors(context);
    await SeedData.SeedAsync(context);
    SeedData.SeedEnrollee(context);
    SeedData.SeedEncounters(context);
    SeedData.SeedClaims(context);
    await SeedData.SeedAdminUser(services);

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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CTSHIPDashboard.Middleware.UserActivityMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Analytics}/{action=Index}/{id?}");

app.MapHub<AnalyticsHub>("/analyticsHub");
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
