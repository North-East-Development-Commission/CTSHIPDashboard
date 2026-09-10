using System.Security.Claims;
using CTSHIPDashboard.Controllers;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
Check(VulnerabilityClassification.IsPregnant(false, " Pregnant Woman "), "legacy pregnancy category");
Check(VulnerabilityClassification.IsPregnant(true, null), "pregnancy checkbox");
Check(!VulnerabilityClassification.IsPregnant(false, "Not pregnant"), "negative pregnancy category");
Check(!VulnerabilityClassification.IsPregnant(false, "IDP"), "unrelated vulnerable category");
Check(VulnerabilityClassification.HasDisability(false, " PLWD "), "legacy PLWD abbreviation");
Check(VulnerabilityClassification.HasDisability(false, " Physically   Challenged "), "physically challenged maps to PLWD regardless of case and spacing");
Check(VulnerabilityClassification.HasDisability(false, "Person Living with Disability"), "legacy disability full name");
Check(VulnerabilityClassification.HasDisability(false, "Person Living with Disability (PLWD)"), "legacy disability combined label");
Check(VulnerabilityClassification.HasDisability(true, null), "disability checkbox");
Check(!VulnerabilityClassification.HasDisability(false, "No disability"), "negative disability category");

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var db = new TestContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync();
var services = new ServiceCollection();
services.AddLogging();
services.AddControllersWithViews();
services.AddSingleton<ApplicationDbContext>(db);
services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
services.AddDataProtection();
using var provider = services.BuildServiceProvider();
var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
await roles.CreateAsync(new IdentityRole("Provider"));
await roles.CreateAsync(new IdentityRole("HmoEnrollmentOfficer"));
db.Hmos.AddRange(new Hmo { Id = 1, Name = "One", RegistrationNumber = "ONE" }, new Hmo { Id = 2, Name = "Two", RegistrationNumber = "TWO" });
db.Providers.AddRange(
    new Provider { Id = 1, HmoId = 1, Name = "Facility One", State = "Borno", LGA = "Jere", Level = "Primary", Phone = "", Email = "", IsActive = true },
    new Provider { Id = 2, HmoId = 2, Name = "Facility Two", State = "Borno", LGA = "Jere", Level = "Primary", Phone = "", Email = "", IsActive = true });
await db.SaveChangesAsync();
ApplicationUser Account(string id, int hmo) => new() { Id = id, UserName = id + "@example.test", Email = id + "@example.test", FullName = id, State = "Borno", ContactInfo = "", HmoId = hmo };
var owner = Account("owner", 1);
var foreign = Account("foreign", 2);
var unmanaged = Account("unmanaged", 1);
await users.CreateAsync(owner);
await users.CreateAsync(foreign);
await users.CreateAsync(unmanaged);
await users.AddClaimAsync(foreign, new System.Security.Claims.Claim("CreatedByHmo", "2"));
HmoUsersController Controller()
{
    var http = new DefaultHttpContext { RequestServices = provider,
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, owner.Id), new System.Security.Claims.Claim(ClaimTypes.Role, "HMO") }, "test")) };
    return new HmoUsersController(db, users) { ControllerContext = new ControllerContext { HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
}
Check(await Controller().Edit(foreign.Id) is NotFoundResult, "cross-HMO user is inaccessible");
Check(await Controller().Edit(unmanaged.Id) is NotFoundResult, "unowned same-HMO user is inaccessible");
HmoUserViewModel Input(int facility, params string[] selectedRoles) => new() { FullName = "Desk Officer", Email = "desk@example.test", ProviderId = facility, Roles = selectedRoles.ToList(), Password = "Test-Only-Password9!" };
var badFacility = Controller();
Check(await badFacility.Edit(Input(2, "Provider")) is ViewResult && !badFacility.ModelState.IsValid, "cross-HMO facility assignment rejected");
var badRole = Controller();
Check(await badRole.Edit(Input(1, "Admin")) is ViewResult && !badRole.ModelState.IsValid, "privileged role assignment rejected");
Check(await Controller().Edit(Input(1, "Provider", "HmoEnrollmentOfficer")) is RedirectToActionResult, "facility officer account created");
var created = await users.FindByEmailAsync("desk@example.test");
Check(created?.HmoId == 1 && created.ProviderId == 1, "created account keeps HMO and facility scope");
Check((await users.GetClaimsAsync(created!)).Any(c => c.Type == "CreatedByHmo" && c.Value == "1"), "creating HMO recorded");
Check((await users.GetRolesAsync(created!)).Count == 2, "allowed facility roles assigned");
var list = (ViewResult)await Controller().Index(null, null);
Check(((List<ApplicationUser>)list.Model!).Count == 1, "account list only includes owned accounts");
var disabled = Input(1, "Provider");
disabled.Id = created!.Id;
disabled.Enabled = false;
disabled.Password = null;
Check(await Controller().Edit(disabled) is RedirectToActionResult, "owned account can be managed");
created = await users.FindByIdAsync(created.Id) ?? throw new Exception("Created account was not persisted.");
Check(await users.IsLockedOutAsync(created), "disabled account is locked out");
Check((await users.GetRolesAsync(created)).SequenceEqual(new[] { "Provider" }), "removed officer role is revoked");
var sink = new TestNotifications();
var referralService = new ReferralService(db, sink, sink);
var hospital = new ReferredHospital { Name = "Facility One", State = "Borno" };
var otherHospital = new ReferredHospital { Name = "Different Facility", State = "Borno" };
db.ReferralHospitals.AddRange(hospital, otherHospital);
var referral = new Referral { ReferredHospital = hospital, Status = CTSHIPDashboard.Enums.ReferralStatus.SubmittedToHmo,
    EnrolleeNumber = "TEST-001", EnrolleeFullName = "Sample Enrollee", FromProviderName = "Origin",
    Diagnosis = "Sample diagnosis", ReasonForReferral = "Specialist review" };
db.Referrals.Add(referral);
await db.SaveChangesAsync();
Check(await referralService.VerifyReferralAsync(new CTSHIPDashboard.ViewModels.ReferralVerificationViewModel
    { ReferralId = referral.Id, IsApproved = true, VerificationNote = "Approved" }, owner.Id, owner.FullName),
    "HMO can approve referral without issuing code");
Check(referral.ReferralVerificationCode == null && referral.ReferralVerificationCodeExpiresAt == null,
    "approved referral has no code or expiry");
Check(!await referralService.ReissueReferralVerificationCodeAsync(referral.Id, owner.Id, owner.FullName),
    "legacy code generation endpoint is disabled");
owner = await users.FindByIdAsync(owner.Id) ?? throw new Exception("Owner account was not persisted.");
owner.ProviderId = 1;
await users.UpdateAsync(owner);
var referralHttp = new DefaultHttpContext { RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, owner.Id),
    new System.Security.Claims.Claim(ClaimTypes.Role, "ReferralPro")
}, "test")) };
var referrals = new ReferralProController(db, referralService, sink, users, null!, sink)
{
    ControllerContext = new ControllerContext { HttpContext = referralHttp,
        RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
        ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() },
    TempData = new TempDataDictionary(referralHttp, new MemoryTempData())
};
Check(await referrals.Receive(referral.Id) is RedirectToActionResult { ActionName: "Encounter" },
    "assigned facility starts encounter without code");
Check(referral.Status == CTSHIPDashboard.Enums.ReferralStatus.Received, "referral reception is recorded");
var otherReferral = new Referral { ReferredHospital = otherHospital, Status = CTSHIPDashboard.Enums.ReferralStatus.Verified,
    EnrolleeNumber = "TEST-002", EnrolleeFullName = "Other Enrollee", FromProviderName = "Origin",
    Diagnosis = "Sample diagnosis", ReasonForReferral = "Specialist review" };
db.Referrals.Add(otherReferral);
await db.SaveChangesAsync();
Check(await referrals.Receive(otherReferral.Id) is ForbidResult, "another facility cannot receive referral");
Console.WriteLine($"{checks} regression checks passed.");

if (args.Contains("--preview")) await PreviewHost.RunAsync(db);

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
sealed class TestContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnType(null);
                property.SetDefaultValueSql(null);
            }
    }
}
