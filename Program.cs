using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

//builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();

//ilder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    //options.Password.RequireDigit = true;
    //options.Password.RequireLowercase = true;
    //options.Password.RequireUppercase = true;
    //options.Password.RequireNonAlphanumeric = false;
   // options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();  // Add this if you want the default Identity UI pages
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();   // <-- ADD THIS LINE!
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));

var app = builder.Build();
// ADD THIS BEFORE ANY EPPlus USAGE!
OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("CTSHIP NEDC Project");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var services = scope.ServiceProvider;

    // 1. Database seeds
    SeedData.SeedHmos(context);
    SeedData.SeedProviders(context);
    SeedData.SeedEnrollee(context);
    //SeedData.Initialize(services);           // 800 Enrollees
    SeedData.SeedEncounters(context);
    SeedData.SeedClaims(context);

    // 2. ADMIN & ROLES (MUST BE LAST � uses UserManager)
    await SeedData.SeedAdminUser(services);

    Console.WriteLine("CTSHIP FULLY SEEDED + ADMIN READY!");
    Console.WriteLine("SUPER ADMIN: admin@nhia.gov.ng / Nigeria@2025!");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();
// ADD THIS LINE � AUTOMATIC ACTIVITY TRACKING
app.UseMiddleware<CTSHIPDashboard.Middleware.UserActivityMiddleware>();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Analytics}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<AnalyticsHub>("/analyticsHub");
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
