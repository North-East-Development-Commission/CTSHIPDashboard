using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Data;
using System.Globalization;

namespace CTSHIPDashboard.Controllers;

public partial class EncountersController
{
    private const string EncounterUploadRoles = "CTSHIPAdmin,Admin,HMO";
    private const int EncounterUploadMaxRows = 500;
    private const int EncounterUploadMaxBytes = 5 * 1024 * 1024;
    private const int EncounterUploadRequiredColumnCount = 8;
    private static readonly string[] EncounterUploadHeaders =
    [
        "EnrollmentNumber", "VisitDate", "ChiefComplaint", "Diagnosis", "TreatmentGiven",
        "ReasonForEncounter", "ServiceSetting", "ServicesProvided", "PatientOutcome", "Notes"
    ];

    [HttpGet]
    [Authorize(Roles = EncounterUploadRoles)]
    public async Task<IActionResult> BulkUpload(int hmoId, int providerId, CancellationToken cancellationToken)
    {
        int? scopedHmoId = null;
        if (!User.IsInRole("CTSHIPAdmin") && !User.IsInRole("Admin"))
        {
            scopedHmoId = (await _userManager.GetUserAsync(User))?.HmoId;
            if (!scopedHmoId.HasValue || (hmoId != 0 && hmoId != scopedHmoId.Value))
                return Forbid();
            hmoId = scopedHmoId.Value;
        }
        ViewBag.Hmos = new SelectList(await _context.Hmos.AsNoTracking()
            .Where(hmo => !scopedHmoId.HasValue || hmo.Id == scopedHmoId.Value)
            .OrderBy(hmo => hmo.Name).ToListAsync(cancellationToken), "Id", "Name", hmoId);
        ViewBag.Facilities = await _context.Providers.AsNoTracking()
            .Where(facility => facility.IsActive && (!scopedHmoId.HasValue || facility.HmoId == scopedHmoId.Value))
            .OrderBy(facility => facility.Name)
            .Select(facility => new EncounterUploadFacility(facility.Id, facility.HmoId, facility.Name, facility.State, facility.LGA))
            .ToListAsync(cancellationToken);
        ViewBag.SelectedHmoId = hmoId;
        ViewBag.SelectedProviderId = providerId;
        return View();
    }

    [HttpGet]
    [Authorize(Roles = EncounterUploadRoles)]
    public IActionResult DownloadBulkUploadTemplate()
    {
        using var workbook = new ExcelPackage();
        var sheet = workbook.Workbook.Worksheets.Add("Encounters");
        for (int column = 0; column < EncounterUploadHeaders.Length; column++)
        {
            sheet.Cells[1, column + 1].Value = EncounterUploadHeaders[column];
            sheet.Column(column + 1).Width = 24;
            if (column >= EncounterUploadRequiredColumnCount)
            {
                string note = EncounterUploadHeaders[column] == "PatientOutcome"
                    ? "Optional. A blank value defaults to Treated."
                    : "Optional. Add any relevant notes.";
                sheet.Cells[1, column + 1].AddComment(note, "CTSHIP");
            }
        }
        sheet.Cells[1, 1, 1, EncounterUploadHeaders.Length].Style.Font.Bold = true;
        sheet.Column(1).Style.Numberformat.Format = "@";
        sheet.Column(2).Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
        sheet.View.FreezePanes(2, 1);

        var guide = workbook.Workbook.Worksheets.Add("Allowed values");
        string[][] lists =
        [
            EncounterReasonCatalog.All.ToArray(),
            [EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Inpatient],
            EncounterLookups.ServicesProvided.Where(IsBulkEncounterService).ToArray(),
            EncounterLookups.PatientOutcomes.Where(IsBulkEncounterOutcome).ToArray()
        ];
        for (int column = 0; column < lists.Length; column++)
        {
            guide.Cells[1, column + 1].Value = EncounterUploadHeaders[column + 5];
            guide.Column(column + 1).Width = 40;
            for (int row = 0; row < lists[column].Length; row++)
                guide.Cells[row + 2, column + 1].Value = lists[column][row];
        }
        guide.Cells[1, 6].Value = "Optional fields";
        guide.Cells[2, 6].Value = "PatientOutcome defaults to Treated when blank.";
        guide.Cells[3, 6].Value = "Notes can be left blank.";
        guide.Cells[5, 6].Value = "Use one row per visit. Repeat EnrollmentNumber for each visit by the same patient.";
        guide.Cells[6, 6].Value = "Include the time when a patient has multiple visits on the same date.";
        guide.Cells[5, 6, 6, 6].Style.WrapText = true;
        guide.Row(5).Height = 32;
        guide.Row(6).Height = 32;
        guide.Column(6).Width = 52;

        return File(workbook.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EncounterUploadTemplate.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = EncounterUploadRoles)]
    [RequestSizeLimit(EncounterUploadMaxBytes + 64 * 1024)]
    public async Task<IActionResult> BulkUpload(IFormFile? excelFile, int hmoId, int providerId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();
        if (!User.IsInRole("CTSHIPAdmin") && !User.IsInRole("Admin")
            && (!user.HmoId.HasValue || user.HmoId.Value != hmoId))
            return Forbid();

        async Task<IActionResult> ShowErrorsAsync(IReadOnlyList<string> errors)
        {
            ViewBag.UploadErrors = errors.Take(50).ToArray();
            ViewBag.UploadErrorCount = errors.Count;
            return await BulkUpload(hmoId, providerId, cancellationToken);
        }

        if (excelFile == null || excelFile.Length == 0 || excelFile.Length > EncounterUploadMaxBytes
            || !string.Equals(Path.GetExtension(excelFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return await ShowErrorsAsync(["Select an Excel .xlsx file up to 5 MB."]);

        var rows = new List<(int Row, string EnrollmentNumber, Encounter Visit)>();
        var errors = new List<string>();
        try
        {
            using var stream = excelFile.OpenReadStream();
            using var workbook = new ExcelPackage(stream);
            var sheet = workbook.Workbook.Worksheets.FirstOrDefault();
            if (sheet?.DimensionByValue == null)
                return await ShowErrorsAsync(["The first worksheet is empty."]);
            if (sheet.DimensionByValue.End.Row > EncounterUploadMaxRows + 1 || sheet.DimensionByValue.End.Column > 64)
                return await ShowErrorsAsync(["Use up to 500 encounter rows and the columns in the template."]);

            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int column = 1; column <= sheet.DimensionByValue.End.Column; column++)
            {
                string header = sheet.Cells[1, column].Text.Trim();
                if (header.Length > 0 && !columns.TryAdd(header, column))
                    return await ShowErrorsAsync([$"The worksheet contains a duplicate header in column {column}."]);
            }

            var missing = EncounterUploadHeaders.Take(EncounterUploadRequiredColumnCount)
                .Where(header => !columns.ContainsKey(header)).ToArray();
            if (missing.Length > 0)
                return await ShowErrorsAsync([$"Missing columns: {string.Join(", ", missing)}."]);

            for (int row = 2; row <= sheet.DimensionByValue.End.Row; row++)
            {
                string Read(string header) => columns.TryGetValue(header, out int column)
                    ? sheet.Cells[row, column].Text.Trim() : string.Empty;
                if (EncounterUploadHeaders.All(header => Read(header).Length == 0))
                    continue;

                string enrollmentNumber = Read("EnrollmentNumber");
                if (enrollmentNumber.Length is 0 or > 30)
                    errors.Add($"Row {row}: Enter a valid enrolment number.");
                if (!TryReadEncounterDate(sheet.Cells[row, columns["VisitDate"]], out DateTime visitDate))
                    errors.Add($"Row {row}: Enter a valid visit date and time that is not in the future.");

                string reason = Read("ReasonForEncounter");
                string setting = Read("ServiceSetting");
                string outcome = Read("PatientOutcome");
                var visit = new Encounter
                {
                    ProviderId = providerId,
                    VisitDate = TrimToSecond(visitDate),
                    ChiefComplaint = Read("ChiefComplaint"),
                    Diagnosis = Read("Diagnosis"),
                    TreatmentGiven = Read("TreatmentGiven"),
                    ReasonForEncounter = EncounterReasonCatalog.All.FirstOrDefault(value => SameValue(value, reason)) ?? "",
                    ServiceSetting = new[] { EncounterServiceCatalog.Outpatient, EncounterServiceCatalog.Inpatient }
                        .FirstOrDefault(value => SameValue(value, setting)) ?? "",
                    PatientOutcome = outcome.Length == 0 ? "Treated"
                        : EncounterLookups.PatientOutcomes.FirstOrDefault(value => SameValue(value, outcome) && IsBulkEncounterOutcome(value)) ?? "",
                    SelectedServices = Read("ServicesProvided").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(service => EncounterLookups.ServicesProvided.FirstOrDefault(value => SameValue(value, service)) ?? service)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    Notes = Read("Notes"),
                    AttendedBy = user.FullName ?? user.Email
                };

                if (visit.ChiefComplaint.Length == 0 || visit.Diagnosis.Length == 0 || visit.TreatmentGiven.Length == 0)
                    errors.Add($"Row {row}: Chief complaint, diagnosis and treatment given are required.");
                if (visit.ReasonForEncounter.Length == 0 || visit.ServiceSetting.Length == 0 || visit.PatientOutcome.Length == 0)
                    errors.Add($"Row {row}: Use the allowed values for reason, service setting and patient outcome.");
                if (visit.SelectedServices.Count == 0 || visit.SelectedServices.Any(service =>
                    !IsBulkEncounterService(service) || !EncounterServiceCatalog.IsValid(visit.ServiceSetting, service)))
                    errors.Add($"Row {row}: Enter allowed services separated by semicolons. Record referrals and medicine dispensing individually.");

                rows.Add((row, enrollmentNumber, visit));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or InvalidOperationException
            or System.Xml.XmlException or OfficeOpenXml.Packaging.Ionic.Zip.ZipException)
        {
            return await ShowErrorsAsync(["The workbook could not be read. Save it as an unprotected .xlsx file and try again."]);
        }

        if (rows.Count == 0)
            errors.Add("Enter at least one encounter row.");
        if (errors.Count > 0)
            return await ShowErrorsAsync(errors);

        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var facility = await _context.Providers.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.Id == providerId && p.HmoId == hmoId && p.IsActive, cancellationToken);
                if (facility == null)
                {
                    errors.Add("Select an active facility that belongs to the selected HMO.");
                    return;
                }

                var numbers = rows.Select(row => row.EnrollmentNumber.ToUpperInvariant()).Distinct().ToArray();
                var enrollees = await _context.Enrollees.AsNoTracking()
                    .Include(e => e.Hmo)
                    .Where(e => e.ProviderId == providerId && e.HmoId == hmoId && numbers.Contains(e.EnrollmentNumber.ToUpper()))
                    .ToListAsync(cancellationToken);
                var byNumber = enrollees.ToLookup(e => e.EnrollmentNumber, StringComparer.OrdinalIgnoreCase);
                var enrolleeIds = enrollees.Select(e => e.Id).ToArray();
                var dates = rows.Select(row => row.Visit.VisitDate).Distinct().ToArray();
                var existing = await _context.Encounters.AsNoTracking()
                    .Where(visit => visit.ProviderId == providerId && enrolleeIds.Contains(visit.EnrolleeId)
                        && dates.Contains(visit.VisitDate))
                    .Select(visit => new { visit.EnrolleeId, visit.VisitDate, visit.ChiefComplaint, visit.Diagnosis, visit.TreatmentGiven })
                    .ToListAsync(cancellationToken);
                var visits = existing.Select(visit => (visit.EnrolleeId, visit.VisitDate, visit.ChiefComplaint.Trim(),
                    visit.Diagnosis?.Trim(), visit.TreatmentGiven?.Trim())).ToHashSet();

                foreach (var row in rows)
                {
                    var matches = byNumber[row.EnrollmentNumber];
                    if (matches.Count() != 1)
                    {
                        errors.Add($"Row {row.Row}: The enrolment number must identify one enrollee assigned to the selected HMO and facility.");
                        continue;
                    }

                    var visit = row.Visit;
                    visit.EnrolleeId = matches.Single().Id;
                    if (!visits.Add((visit.EnrolleeId, visit.VisitDate, visit.ChiefComplaint, visit.Diagnosis, visit.TreatmentGiven)))
                        errors.Add($"Row {row.Row}: This encounter duplicates an existing visit or another row.");
                }

                if (errors.Count > 0)
                    return;

                int sequence = await _context.Encounters.MaxAsync(visit => (int?)visit.Id, cancellationToken) ?? 0;
                try
                {
                    foreach (var row in rows)
                    {
                        var visit = row.Visit;
                        visit.EncounterNumber = $"ECN-{CtshipClock.Today:yyyy}-{++sequence:D6}";
                        SetEncounterServices(visit);
                        await ApplyPrimaryProviderCapitationSubmissionAsync(visit, byNumber[row.EnrollmentNumber].Single(), facility, user);
                        _context.Encounters.Add(visit);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    _context.ChangeTracker.Clear();
                    foreach (var row in rows)
                    {
                        row.Visit.Id = 0;
                        row.Visit.Services.Clear();
                        row.Visit.AuditTrails.Clear();
                    }
                    throw;
                }
            });
        }
        catch (Exception exception) when (exception is DbUpdateException or System.Data.Common.DbException
            or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<EncountersController>>()
                .LogError(exception, "Bulk encounter upload failed for facility {ProviderId}", providerId);
            ViewBag.UploadUnconfirmed = true;
            return await ShowErrorsAsync(["The upload could not be confirmed. Check the encounter list before trying again."]);
        }

        if (errors.Count > 0)
            return await ShowErrorsAsync(errors);

        await _auditService.LogAsync("Encounter.BulkUploaded", AuditActor.Format(user, User.Identity?.Name),
            details: AuditActor.Details($"Provider:{providerId}", $"HMO:{hmoId}", $"Imported:{rows.Count}"),
            cancellationToken: cancellationToken);
        TempData["Success"] = $"{rows.Count} encounter(s) imported.";
        return RedirectToAction(nameof(BulkUpload), new { hmoId, providerId });
    }

    private static bool TryReadEncounterDate(ExcelRangeBase cell, out DateTime value)
    {
        if (cell.Value is DateTime date)
            value = date;
        else if (cell.Value is double number && number >= 1 && number < 2958466)
            value = DateTime.FromOADate(number);
        else if (!DateTime.TryParseExact(cell.Text.Trim(),
            ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return false;

        return value.Year >= 1900 && value <= CtshipClock.Now;
    }

    private static bool SameValue(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsBulkEncounterService(string service) =>
        !SameValue(service, EncounterServiceCatalog.PrescriptionService) && !SameValue(service, "Referral Services");

    private static bool IsBulkEncounterOutcome(string outcome) => !SameValue(outcome, "Referred");

    public sealed record EncounterUploadFacility(int Id, int HmoId, string Name, string State, string Lga);
}
