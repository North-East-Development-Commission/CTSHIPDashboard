using System.Security.Claims;
using System.Text.Json;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;

static class EnrollmentAddressChecks
{
    public static async Task RunStandaloneAsync(Action<bool, string> check, bool preview = false)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews();
        services.AddSingleton<ApplicationDbContext>(db);
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = new ApplicationUser { Id = "address-owner", UserName = "address@example.test",
            Email = "address@example.test", FullName = "Enrollment Officer", State = "Borno", ContactInfo = "", HmoId = 1 };
        db.Hmos.Add(new Hmo { Id = 1, Name = "Test HMO", RegistrationNumber = "ADDRESS-HMO" });
        db.Providers.Add(new Provider { Id = 1, HmoId = 1, Name = "Test Facility", State = "Borno", LGA = "Jere",
            Level = "Primary", Phone = "", Email = "", IsActive = true });
        await db.SaveChangesAsync();
        await users.CreateAsync(owner);
        await RunAsync(db, users, provider, owner, new TestNotifications(), check);
        if (preview) await PreviewHost.RunAsync(db);
    }

    public static async Task RunAsync(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IServiceProvider services, ApplicationUser owner, IAuditService audit, Action<bool, string> check)
    {
        var metadata = services.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForProperty(typeof(Enrollee), nameof(Enrollee.Address));
        check(!metadata.IsRequired, "Address is optional in MVC form validation");
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        check(!metadataProvider.GetMetadataForProperty(typeof(Enrollee), nameof(Enrollee.NIN)).IsRequired
            && !metadataProvider.GetMetadataForProperty(typeof(Enrollee), nameof(Enrollee.Phone)).IsRequired,
            "NIN and phone number are optional in MVC form validation");

        ControllerContext Context() => new()
        {
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor(),
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, owner.Id),
                    new System.Security.Claims.Claim(ClaimTypes.Role, "HMO")
                }, "test"))
            }
        };
        Enrollee Input(string? address) => new()
        {
            FullName = "Optional Address Enrollee", EnrollmentNumber = "TEST-ADDRESS",
            Gender = "Female", DateOfBirth = new DateTime(1992, 4, 18),
            State = "Borno", LGA = "Jere", Ward = "Test Ward",
            HmoId = 1, ProviderId = 1, Address = address
        };
        EnrolleesController Controller()
        {
            var controller = new EnrolleesController(db, users, null!, null!, audit) { ControllerContext = Context() };
            controller.TempData = new TempDataDictionary(controller.HttpContext, new MemoryTempData());
            return controller;
        }
        foreach (var input in new[] { (Address: (string?)null, Gender: " f "), (Address: "", Gender: "M"), (Address: "   ", Gender: "female") })
        {
            var controller = Controller();
            var enrollee = Input(input.Address);
            enrollee.Gender = input.Gender;
            check(controller.TryValidateModel(enrollee), "create validation accepts missing or blank Address");
            check(await controller.Create(enrollee) is RedirectToActionResult && enrollee.Id > 0,
                "create saves enrollee with missing or blank Address");
            check(await db.Enrollees.AsNoTracking().Where(e => e.Id == enrollee.Id).Select(e => e.Address).SingleAsync() == "",
                "blank Address persists in existing database column");
            check(enrollee.Phone == null && enrollee.NIN == null, "create accepts missing phone and NIN");
            check(enrollee.Gender == (input.Gender == "M" ? "Male" : "Female"), "create normalizes gender abbreviations and casing");
        }
        var hmo = new HmoController(db, users, null!, null!, audit) { ControllerContext = Context() };
        hmo.TempData = new TempDataDictionary(hmo.HttpContext, new MemoryTempData());
        var hmoEnrollee = Input(null);
        hmoEnrollee.Gender = " m ";
        check(hmo.TryValidateModel(hmoEnrollee) && await hmo.AddEnrollee(hmoEnrollee) is RedirectToActionResult
            && hmoEnrollee.Id > 0 && hmoEnrollee.Address == "", "HMO create accepts and saves missing Address");
        check(hmoEnrollee.Gender == "Male", "HMO create normalizes M to Male");
        check(!Controller().TryValidateModel(Input(new string('a', 501))), "Address length limit remains enforced");
        var invalidPhone = Input(null);
        invalidPhone.Phone = "not-a-phone";
        var phoneController = Controller();
        check(!phoneController.TryValidateModel(invalidPhone), "provided invalid phone number remains rejected");
        check(phoneController.ModelState[nameof(Enrollee.Phone)]!.Errors.Any(e => e.ErrorMessage.Contains("valid phone number")),
            "invalid phone error is specific to the Phone field");
        var invalidNin = Input(null);
        invalidNin.NIN = 0;
        check(!Controller().TryValidateModel(invalidNin), "provided invalid NIN remains rejected");

        ExcelPackage.License.SetNonCommercialPersonal("CTSHIP NEDC Project");
        foreach (bool includeAddress in new[] { true, false })
        {
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Enrollees");
            string[] headers = { "EnrollmentNumber", "FullName", "Gender", "DateOfBirth", "State", "LGA", "Ward" };
            string gender = includeAddress ? " f " : " m ";
            object[] values = { "", "Bulk Optional Address", gender, new DateTime(1992, 4, 18), "Borno", "Jere", "Test Ward" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[2, i + 1].Value = values[i];
            }
            if (includeAddress)
            {
                sheet.Cells[1, headers.Length + 1].Value = "Address";
                sheet.Cells[1, headers.Length + 2].Value = "Phone";
                sheet.Cells[1, headers.Length + 3].Value = "NIN";
            }
            sheet.Cells[2, 4].Style.Numberformat.Format = "dd/mm/yyyy";
            using var stream = new MemoryStream(package.GetAsByteArray());
            var file = new FormFile(stream, 0, stream.Length, "excelFile", "enrollees.xlsx");
            var controller = Controller();
            int before = await db.Enrollees.CountAsync();
            await controller.BulkUpload(file, 1, 1);
            check(controller.TempData.ContainsKey("Success") && await db.Enrollees.CountAsync() == before + 1,
                (includeAddress ? "bulk import accepts blank Address, Phone, and NIN cells" : "bulk import accepts omitted Address, Phone, and NIN columns")
                    + $" (errors: {controller.TempData.Peek("Error")} {controller.TempData.Peek("ErrorDetails")})");
            var imported = await db.Enrollees.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
            check(imported.Gender == (includeAddress ? "Female" : "Male") && imported.Phone == null && imported.NIN == null,
                "bulk import normalizes gender and saves optional phone and NIN");
        }
        using (var package = new ExcelPackage())
        {
            var sheet = package.Workbook.Worksheets.Add("Enrollees");
            string[] headers = { "NIN", "FullName", "Ward", "Address", "Gender", "DateOfBirth", "State", "LGA", "Phone", "EnrollmentNumber", "DateRegistered" };
            object[] invalid = { "bad-nin", "", "", new string('a', 501), "X", "bad-date", "Borno", "Wrong LGA", "bad-phone", "", "bad-date" };
            string existingNumber = await db.Enrollees.Select(e => e.EnrollmentNumber).FirstAsync();
            object[] duplicate = { "", "Duplicate Enrollee", "Test Ward", "", "M", "18/04/1992", "Borno", "Jere", "", existingNumber, "" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[2, i + 1].Value = invalid[i];
                sheet.Cells[3, i + 1].Value = duplicate[i];
            }
            using var stream = new MemoryStream(package.GetAsByteArray());
            var controller = Controller();
            int before = await db.Enrollees.CountAsync();
            await controller.BulkUpload(new FormFile(stream, 0, stream.Length, "excelFile", "errors.xlsx"), 1, 1);
            var errors = JsonSerializer.Deserialize<List<BulkEnrolleeUploadError>>(
                controller.TempData.Peek("BulkUploadErrors") as string ?? "[]")!;
            foreach (var expected in new[] { (Field: "NIN", Cell: "A2"), (Field: "FullName", Cell: "B2"),
                (Field: "Ward", Cell: "C2"), (Field: "Address", Cell: "D2"), (Field: "Gender", Cell: "E2"),
                (Field: "DateOfBirth", Cell: "F2"), (Field: "LGA", Cell: "H2"), (Field: "Phone", Cell: "I2"),
                (Field: "DateRegistered", Cell: "K2"), (Field: "EnrollmentNumber", Cell: "J3") })
            {
                check(errors.Any(e => e.Field == expected.Field && e.Cell == expected.Cell && e.Message.Length > 0),
                    $"bulk error identifies {expected.Field} at {expected.Cell}");
            }
            check(await db.Enrollees.CountAsync() == before, "invalid bulk rows are not imported");
            check((controller.TempData.Peek("Error") as string)?.Contains("2 row(s)") == true,
                "bulk summary distinguishes issue count from affected row count");
        }
        var missingFile = Controller();
        await missingFile.BulkUpload(null!, 1, 1);
        check(missingFile.TempData.ContainsKey("ExcelFileError"), "missing file has an inline file-field error");
        await EnrollmentGenderChecks.RunAsync(db, check);
    }
}
