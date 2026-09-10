using System.Security.Claims;
using CTSHIPDashboard.Controllers;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

internal static class PreviewHost
{
    public static async Task RunAsync(ApplicationDbContext database)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Directory.GetCurrentDirectory(),
            WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5098");
        builder.Services.AddSingleton(database);
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HmoController).Assembly);
        var app = builder.Build();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/preview"))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "owner"),
                new System.Security.Claims.Claim(ClaimTypes.Name, "Sample HMO Officer"),
                new System.Security.Claims.Claim(ClaimTypes.Role, "HMO")
            }, IdentityConstants.ApplicationScheme));
            await next();
        });
        app.MapControllerRoute("preview", "preview/{action=Dashboard}", new { controller = "Preview" });
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        Console.WriteLine("Sample-data preview: http://127.0.0.1:5098/preview/dashboard");
        await app.RunAsync();
    }
}

public class PreviewController : Controller
{
    public IActionResult Dashboard(string? state, string? lga)
    {
        ViewBag.DashboardTitle = "HMO Dashboard";
        ViewBag.DashboardHeading = "Sample HMO";
        ViewBag.LgaController = "Preview";
        ViewBag.DashboardProviders = Enumerable.Range(1, 5).Select(i => new Provider
        {
            Id = i, Name = $"Sample Facility {i}", State = "Borno", LGA = "Jere",
            Level = i == 5 ? "Secondary" : "Primary", IsActive = i != 4
        }).ToList();
        var model = new MonitoringDashboardViewModel
        {
            Scope = "Sample data", ScopeDisplay = "Sample data", SelectedState = state ?? "", SelectedLga = lga ?? "",
            AvailableStates = new() { "Adamawa", "Borno", "Yobe" }, AvailableLgas = new() { "Jere", "Maiduguri" },
            TotalEnrolled = 12480, ActiveEnrollees = 11232, ActiveEnrolleeRate = 90,
            TargetEnrollees = 15000, CoveragePercentage = 74.9m,
            MaleCount = 5741, FemaleCount = 6739, MalePercentage = 46, FemalePercentage = 54,
            TotalEncounters = 5834, TotalVisits = 4621, TotalProviders = 87,
            TotalReferrals = 320, CompletedReferrals = 264, PendingReferrals = 56, ReferralCompletionRate = 82.5m,
            TotalClaims = 1842, ClaimsValidated = 1601, RejectedClaims = 42, OutstandingClaims = 199,
            TotalClaimValue = 142800000, ApprovedClaimValue = 131000000, PaidClaimValue = 119000000,
            OutstandingClaimValue = 23800000, AverageProcessingDays = 4.2, PaidClaims = 1500,
            PregnantWomenCount = 842, PregnantWomenPercentage = 6.7m,
            ServiceUtilizationRate = 41.2m, UniqueServiceUsers = 4628, ActiveUsersLast30Days = 73,
            StateIndicators = new() { new() { State = "Borno", TotalEnrollees = 12480, ActiveEnrollees = 11232, Encounters = 5834 } },
            HmoOversight = new() { new() { Name = "Sample HMO", Enrollees = 12480, Providers = 87, Encounters = 5834 } },
            ProviderLevelMetrics = new() { new() { Level = "Primary", Providers = 65, Enrollees = 9840, Encounters = 4210 }, new() { Level = "Secondary", Providers = 22, Enrollees = 2640, Encounters = 1624 } },
            VulnerableDistribution = new() { new() { Label = "Pregnant women", Count = 842, Percentage = 6.7m } }
        };
        return View("~/Views/Monitoring/Index.cshtml", model);
    }

    public IActionResult Enrolment() => View("~/Views/Enrollees/Create.cshtml", new Enrollee());
    public IActionResult Lgas(string? state) => Json(new[] { "Jere", "Maiduguri" });
}
