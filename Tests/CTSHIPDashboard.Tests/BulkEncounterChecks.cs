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

        string uploadRole = "CTSHIPAdmin";
        async Task<IActionResult> UploadAsync(int hmoId, params string[] numbers)
        {
            var controller = CreateController(uploadRole);
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

        db.Hmos.Add(new Hmo { Id = 2, Name = "Other HMO", RegistrationNumber = "OTHER-HMO" });
        var otherHmoEnrollee = NewEnrollee("TEST-OTHER-HMO", 1);
        otherHmoEnrollee.HmoId = 2;
        db.Enrollees.Add(otherHmoEnrollee);
        await db.SaveChangesAsync();
        var otherHmoVisit = new Encounter { EnrolleeId = otherHmoEnrollee.Id, ProviderId = 1,
            EncounterNumber = "OTHER-HMO-VISIT", ChiefComplaint = "Other HMO complaint" };
        var otherFacilityVisit = new Encounter { EnrolleeId = foreign.Id, ProviderId = 2,
            EncounterNumber = "OWN-HMO-OTHER-FACILITY", ChiefComplaint = "Own HMO complaint" };
        db.Encounters.AddRange(otherHmoVisit, otherFacilityVisit);
        await db.SaveChangesAsync();
        owner.HmoId = 1;
        await users.UpdateAsync(owner);
        var hmoViewer = CreateController("HMO");
        var list = (ViewResult)await hmoViewer.Index();
        var visible = ((IEnumerable<Encounter>)list.Model!).ToArray();
        check(visible.Length > 0 && visible.All(visit => visit.Enrollee!.HmoId == 1)
            && visible.Any(visit => visit.Id == otherFacilityVisit.Id)
            && (int)list.ViewData["TotalItems"]! == visible.Length,
            "HMO encounter list and total include only its enrollees across facilities");
        check(list.ViewData["CanManageEncounters"] is false, "HMO encounter list hides manual management controls");
        var facilityList = (ViewResult)await hmoViewer.Index(providerId: 2);
        var facilityVisits = ((IEnumerable<Encounter>)facilityList.Model!).ToArray();
        check(facilityVisits.Length == 1 && facilityVisits[0].Id == otherFacilityVisit.Id
            && (int)facilityList.ViewData["TotalItems"]! == 1
            && (int)facilityList.ViewData["ProviderId"]! == 2,
            "HMO facility filter scopes records and totals and retains the selection");
        var facilitySearch = (ViewResult)await hmoViewer.Index(search: foreign.EnrollmentNumber, status: "Treated", providerId: 2);
        check(((IEnumerable<Encounter>)facilitySearch.Model!).Single().Id == otherFacilityVisit.Id,
            "HMO facility filter combines with enrollee search and patient outcome");
        var unknownFacility = (ViewResult)await hmoViewer.Index(providerId: 999999);
        check(!((IEnumerable<Encounter>)unknownFacility.Model!).Any(), "unknown facility filter returns no encounters");
        owner.HmoId = 2;
        await users.UpdateAsync(owner);
        var otherHmoList = (ViewResult)await hmoViewer.Index(providerId: 2);
        check(!((IEnumerable<Encounter>)otherHmoList.Model!).Any()
            && !((Microsoft.AspNetCore.Mvc.Rendering.SelectList)otherHmoList.ViewData["Facilities"]!)
                .Any(facility => facility.Value == "2"),
            "HMO facility options and forged facility filters cannot expose another HMO's encounters");
        owner.HmoId = 1;
        await users.UpdateAsync(owner);
        var searched = (ViewResult)await hmoViewer.Index("TEST-OTHER-HMO");
        check(!((IEnumerable<Encounter>)searched.Model!).Any()
            && (int)searched.ViewData["TotalItems"]! == 0, "HMO search cannot expose another HMO's enrollee");
        check(await hmoViewer.Details(otherFacilityVisit.Id) is ViewResult,
            "HMO can view encounter details for its enrollee at another facility");
        check(await hmoViewer.Details(otherHmoVisit.Id) is NotFoundResult,
            "HMO cannot open another HMO's encounter by ID even at the same facility");
        var hmoUploadEnrollee = NewEnrollee("TEST-HMO-UPLOAD", 1);
        db.Enrollees.Add(hmoUploadEnrollee);
        await db.SaveChangesAsync();
        uploadRole = "HMO";
        check(await UploadAsync(1, hmoUploadEnrollee.EnrollmentNumber) is RedirectToActionResult
            && await db.Encounters.AnyAsync(visit => visit.EnrolleeId == hmoUploadEnrollee.Id),
            "HMO retains successful bulk encounter upload for its own enrollee");

        foreach (var action in new[] { "Index", "Details", "BulkUpload", "DownloadBulkUploadTemplate", "Create", "Edit", "Delete", "RespondEncounterQuery", "SearchEnrollee" })
        {
            hmoViewer.ControllerContext.ActionDescriptor.RouteValues["action"] = action;
            var filters = new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>();
            var executing = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(
                hmoViewer.ControllerContext, filters, new Dictionary<string, object?>(), hmoViewer);
            bool reachedAction = false;
            await hmoViewer.OnActionExecutionAsync(executing, () =>
            {
                reachedAction = true;
                return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(
                    hmoViewer.ControllerContext, filters, hmoViewer));
            });
            bool allowed = action is "Index" or "Details" or "BulkUpload" or "DownloadBulkUploadTemplate";
            check(reachedAction == allowed && (allowed || executing.Result is ForbidResult),
                $"HMO action access: {action} {(allowed ? "allowed" : "blocked")}");
        }
        owner.HmoId = null;
        await users.UpdateAsync(owner);
        check(await hmoViewer.Index() is ForbidResult && await hmoViewer.Details(otherFacilityVisit.Id) is ForbidResult,
            "HMO without an assignment cannot list or view patient encounters");
        check(await CreateController().Details(otherHmoVisit.Id) is ViewResult,
            "administrator retains cross-HMO encounter detail access");
        owner.HmoId = originalHmoId;
        await users.UpdateAsync(owner);
    }

    private static Enrollee NewEnrollee(string number, int facilityId) => new()
    {
        FullName = "Test Encounter Enrollee", EnrollmentNumber = number, Gender = "Female",
        DateOfBirth = new DateTime(1990, 1, 1), State = "Borno", LGA = "Jere", Ward = "Test",
        HmoId = 1, ProviderId = facilityId
    };
}
