using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

static class BulkUploadChecks
{
    public static async Task RunAsync(ApplicationDbContext db,
        Func<IAuditService?, EnrolleesController> controller, Action<bool, string> check)
    {
        var template = (FileContentResult)controller(null).DownloadBulkUploadTemplate();
        using (var templateStream = new MemoryStream(template.FileContents))
        using (var package = new ExcelPackage(templateStream))
        {
            var sheet = package.Workbook.Worksheets.First();
            check(sheet.Dimension.End.Column == 12
                && sheet.Cells[1, 12].Text == "VulnerabilityCategory",
                "downloaded template contains one vulnerability column");
        }

        var cases = new[]
        {
            (Header: "VulnerabilityCategory", First: "Pregnant women", Second: (string?)null, Pregnant: true, Disability: false, Idp: false, Other: (string?)null),
            (Header: "OtherVulnerableCategory", First: "Physically challenged", Second: (string?)null, Pregnant: false, Disability: true, Idp: false, Other: (string?)null),
            (Header: "VulnerabilityCategory", First: "IDP", Second: "Children under 5", Pregnant: false, Disability: false, Idp: true, Other: "Children under 5"),
            (Header: "VulnerabilityCategory", First: "Aged", Second: "Conflict-affected person", Pregnant: false, Disability: false, Idp: false, Other: "Aged; Conflict-affected person"),
            (Header: "VulnerabilityCategory", First: "Aged", Second: " aged ", Pregnant: false, Disability: false, Idp: false, Other: "Aged"),
            (Header: "VulnerabilityCategory", First: "None", Second: "PLWD", Pregnant: false, Disability: true, Idp: false, Other: (string?)null),
            (Header: "VulnerabilityCategory", First: "No disability", Second: (string?)null, Pregnant: false, Disability: false, Idp: false, Other: "No disability")
        };
        foreach (var item in cases)
        {
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Enrollee Upload");
            string[] headers = { "EnrollmentNumber", "FullName", "Gender", "DateOfBirth", "State", "LGA", "Ward", item.Header };
            object[] values = { "", "Bulk Category Test", "F", "18/04/1992", "Borno", "Jere", "Test Ward", item.First };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[2, i + 1].Value = values[i];
            }
            if (item.Second != null)
            {
                sheet.Cells[1, 9].Value = "OtherVulnerableCategory";
                sheet.Cells[2, 9].Value = item.Second;
            }
            using var stream = new MemoryStream(package.GetAsByteArray());
            var upload = controller(null);
            await upload.BulkUpload(new FormFile(stream, 0, stream.Length, "excelFile", "categories.xlsx"), 1, 1);
            check(upload.TempData.ContainsKey("Success"), "bulk upload accepts canonical, legacy, or both vulnerability headers");
            var saved = await db.Enrollees.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
            check(saved.IsPregnant == item.Pregnant && saved.HasDisability == item.Disability
                && saved.IsIdp == item.Idp && saved.OtherVulnerableCategory == item.Other,
                "merged vulnerability preserves flags and free text");
        }

        using (var source = new MemoryStream(template.FileContents))
        using (var package = new ExcelPackage(source))
        {
            var sheet = package.Workbook.Worksheets.First();
            sheet.Cells[2, 12].Value = new string('a', 101);
            using var stream = new MemoryStream(package.GetAsByteArray());
            var upload = controller(null);
            int count = await db.Enrollees.CountAsync();
            await upload.BulkUpload(new FormFile(stream, 0, stream.Length, "excelFile", "long-category.xlsx"), 1, 1);
            var errors = System.Text.Json.JsonSerializer.Deserialize<List<BulkEnrolleeUploadError>>(
                upload.TempData.Peek("BulkUploadErrors") as string ?? "[]")!;
            check(errors.Any(e => e.Field == "VulnerabilityCategory" && e.Cell == "L2")
                && await db.Enrollees.CountAsync() == count,
                "oversized vulnerability identifies the template cell without partial import");
        }

        using var auditStream = new MemoryStream(template.FileContents);
        var auditUpload = controller(new FailingAudit());
        int before = await db.Enrollees.CountAsync();
        await auditUpload.BulkUpload(new FormFile(auditStream, 0, auditStream.Length, "excelFile", "audit.xlsx"), 1, 1);
        check(await db.Enrollees.CountAsync() == before + 1
            && (auditUpload.TempData.Peek("Success") as string)?.Contains("Do not upload") == true
            && !auditUpload.TempData.ContainsKey("Error"),
            "audit failure after save confirms import and prevents misleading retry");

        using var invalidStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var invalidUpload = controller(null);
        await invalidUpload.BulkUpload(new FormFile(invalidStream, 0, invalidStream.Length, "excelFile", "invalid.xlsx"), 1, 1);
        check((invalidUpload.TempData.Peek("Error") as string)?.Contains("could not be opened") == true
            && await db.Enrollees.CountAsync() == before + 1,
            "invalid workbook reports a file error without importing records");
    }

    private sealed class FailingAudit : IAuditService
    {
        public Task LogAsync(string action, string performedBy, string? target = null,
            string? details = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit storage failure");
    }
}
