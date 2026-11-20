using System.Security.Claims;
using CTSHIPDashboard.Controllers;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

internal static class BulkEncounterChecks
{
    private const string Consultation = "General Outpatient Consultation";

    public static async Task RunAsync(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IServiceProvider services, ApplicationUser owner, Action<bool, string> check)
    {
        db.Providers.Add(new Provider
        {
            Id = 2, HmoId = 1, Name = "Other Facility", State = "Borno", LGA = "Jere",
            Level = "Primary", Phone = "", Email = "", IsActive = true
        });
        var local = NewEnrollee("TEST-ENCOUNTER-LOCAL", 1);
        var foreign = NewEnrollee("TEST-ENCOUNTER-FOREIGN", 2);
        db.Enrollees.AddRange(local, foreign);
        await db.SaveChangesAsync();

        EncountersController CreateController(string role = "CTSHIPAdmin")
        {
            var http = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, owner.Id),
                    new System.Security.Claims.Claim(ClaimTypes.Role, role)
                }, "test"))
            };
            var sink = new TestNotifications();
            return new EncountersController(db, users, sink, sink)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = http,
                    RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                    ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
                },
                TempData = new TempDataDictionary(http, new MemoryTempData())
            };
        }

        async Task<IActionResult> UploadAsync(int hmoId, params string[] numbers)
        {
            var controller = CreateController();
            var template = (FileContentResult)controller.DownloadBulkUploadTemplate();
            using var workbook = new ExcelPackage(new MemoryStream(template.FileContents));
            var sheet = workbook.Workbook.Worksheets["Encounters"];
            for (int i = 0; i < numbers.Length; i++)
            {
                object[] values = [numbers[i], new DateTime(2025, 4, 10).AddMinutes(i), "Headache",
                    "Tension headache", "Rest and hydration", "Acute illness", "Outpatient", Consultation];
                for (int column = 0; column < values.Length; column++)
                    sheet.Cells[i + 2, column + 1].Value = values[column];
            }

            using var stream = new MemoryStream(workbook.GetAsByteArray());
            return await controller.BulkUpload(
                new FormFile(stream, 0, stream.Length, "excelFile", "encounters.xlsx"), hmoId, 1, CancellationToken.None);
        }

        var originalHmoId = owner.HmoId;
        owner.HmoId = 1;
        await users.UpdateAsync(owner);
        var hmoController = CreateController("HMO");
        check(await hmoController.BulkUpload(0, 0, CancellationToken.None) is ViewResult
            && (int)hmoController.ViewData["SelectedHmoId"]! == 1
            && ((IReadOnlyList<EncountersController.EncounterUploadFacility>)hmoController.ViewData["Facilities"]!)
                .All(facility => facility.HmoId == 1), "HMO upload page selects and scopes facilities to the assigned HMO");
        check(await hmoController.BulkUpload(2, 0, CancellationToken.None) is ForbidResult,
            "HMO cannot view another HMO's bulk upload");
        check(await hmoController.BulkUpload(null, 2, 1, CancellationToken.None) is ForbidResult,
            "HMO cannot post encounters for another HMO");
        owner.HmoId = null;
        await users.UpdateAsync(owner);
        check(await hmoController.BulkUpload(0, 0, CancellationToken.None) is ForbidResult,
            "HMO without an assignment cannot use bulk upload");
        owner.HmoId = originalHmoId;
        await users.UpdateAsync(owner);

        var imported = db.Encounters.AsNoTracking().Where(visit => visit.EnrolleeId == local.Id || visit.EnrolleeId == foreign.Id);
        check(await UploadAsync(1, local.EnrollmentNumber, foreign.EnrollmentNumber) is ViewResult mixed
            && ((string[])mixed.ViewData["UploadErrors"]!).Any(error => error.StartsWith("Row 3: The enrolment number"))
            && !await imported.AnyAsync(), "bulk encounter rejects an enrollee assigned to another facility and saves no rows");
        check(await UploadAsync(2, local.EnrollmentNumber) is ViewResult && !await imported.AnyAsync(),
            "bulk encounter rejects a facility outside the posted HMO");
        check(await UploadAsync(1, "UNKNOWN-NUMBER") is ViewResult && !await imported.AnyAsync(),
            "bulk encounter rejects an unknown enrolment number");
        check(await UploadAsync(1, local.EnrollmentNumber.ToLowerInvariant(), local.EnrollmentNumber) is RedirectToActionResult
            && await imported.CountAsync(visit => visit.EnrolleeId == local.Id && visit.ProviderId == 1
                && visit.Services.Any(service => service.ServiceName == Consultation) && visit.SubmittedToHmoAt != null
                && visit.PatientOutcome == "Treated") == 2,
            "bulk encounter imports two visits from the template and submits them for HMO verification");
        check(await UploadAsync(1, local.EnrollmentNumber) is ViewResult && await imported.CountAsync() == 2,
            "re-uploading a previously imported encounter does not create duplicates");
    }

    private static Enrollee NewEnrollee(string number, int facilityId) => new()
    {
        FullName = "Test Encounter Enrollee", EnrollmentNumber = number, Gender = "Female",
        DateOfBirth = new DateTime(1990, 1, 1), State = "Borno", LGA = "Jere", Ward = "Test",
        HmoId = 1, ProviderId = facilityId
    };
}
