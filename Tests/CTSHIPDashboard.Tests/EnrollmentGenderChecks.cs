using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

static class EnrollmentGenderChecks
{
    public static async Task RunAsync(ApplicationDbContext db, Action<bool, string> check)
    {
        var analytics = new AnalyticsController(db, null!);
        var before = (EnrollmentAnalyticsViewModel)((ViewResult)await analytics.Enrollment()).Model!;
        string[] genders = { "M", "male", " m ", "MALE", "F", "female", " f ", "FEMALE", "Other" };
        var enrollees = genders.Select((gender, index) => new Enrollee
        {
            FullName = "Legacy Gender Enrollee", EnrollmentNumber = $"TEST-GENDER-{index}",
            Gender = gender, State = "Taraba", LGA = "Jalingo", Ward = "Test Ward",
            DateOfBirth = new DateTime(1992, 4, 18), IsPregnant = index == 0 || index == 6
        }).ToList();
        db.Enrollees.AddRange(enrollees);
        db.Encounters.AddRange(enrollees.Select(enrollee => new Encounter
        {
            Enrollee = enrollee, ProviderId = 1, ChiefComplaint = "Test encounter"
        }));
        await db.SaveChangesAsync();

        var after = (EnrollmentAnalyticsViewModel)((ViewResult)await analytics.Enrollment()).Model!;
        check(after.MaleCount == before.MaleCount + 4 && after.FemaleCount == before.FemaleCount + 4,
            "enrollment analytics counts legacy M/F, mixed case, and spaces");
        check(after.FemalePercentage == (decimal)Math.Round((double)(before.FemaleCount + 4) / (before.TotalEnrollees + 9) * 100),
            "enrollment analytics female percentage includes F records");

        var matrix = await EncounterDemographicMatrixService.BuildAsync(
            db.Enrollees.Where(e => e.State == "Taraba"),
            db.Encounters.Where(e => e.Enrollee!.State == "Taraba"), "Taraba");
        foreach (string gender in new[] { "Male", "Female" })
        {
            var row = matrix.Rows.Single(r => r.Category == gender);
            check(row.Enrollees == 4 && row.Encounters == 4 && row.EnrolleesWithEncounters == 4,
                $"{gender} demographic matrix includes abbreviated and full gender values");
        }
        check(matrix.Rows.Single(r => r.Category == "Other / Unspecified").Enrollees == 1,
            "unknown gender is not classified as Male or Female");
        check(matrix.Rows.Single(r => r.Category == "Pregnant women").Enrollees == 1,
            "pregnancy matrix recognizes F and excludes Male pregnancy flags");

        var monitoring = await new MonitoringIndicatorService(db).BuildDashboardAsync("Taraba");
        check(monitoring.MaleCount == 4 && monitoring.FemaleCount == 4 && monitoring.OtherGenderCount == 1,
            "monitoring dashboard counts legacy M/F values consistently");
        check(monitoring.MalePercentage == 44.4m && monitoring.FemalePercentage == 44.4m,
            "monitoring gender percentages include abbreviated values");

        db.Encounters.RemoveRange(await db.Encounters.Where(e => e.Enrollee!.State == "Taraba").ToListAsync());
        db.Enrollees.RemoveRange(enrollees);
        await db.SaveChangesAsync();
    }
}
