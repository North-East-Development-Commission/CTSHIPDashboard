using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services
{
    public static class EncounterDemographicMatrixService
    {
        private const int ElderlyAge = 60;

        public static async Task<EncounterDemographicMatrixViewModel> BuildAsync(
            IQueryable<Enrollee> enrolleeQuery,
            IQueryable<Encounter> encounterQuery,
            string scopeLabel,
            CancellationToken cancellationToken = default)
        {
            DateTime today = DateTime.Today;

            List<DemographicProfile> enrollees = await enrolleeQuery
                .AsNoTracking()
                .Select(enrollee => new DemographicProfile(
                    enrollee.Id,
                    enrollee.Gender,
                    enrollee.DateOfBirth,
                    enrollee.IsPregnant,
                    enrollee.HasDisability,
                    enrollee.IsIdp,
                    enrollee.OtherVulnerableCategory))
                .ToListAsync(cancellationToken);

            List<EncounterDemographicFact> encounters = await encounterQuery
                .AsNoTracking()
                .Where(encounter => encounter.Enrollee != null)
                .Select(encounter => new EncounterDemographicFact(
                    encounter.EnrolleeId,
                    encounter.Enrollee!.Gender,
                    encounter.Enrollee.DateOfBirth,
                    encounter.Enrollee.IsPregnant,
                    encounter.Enrollee.HasDisability,
                    encounter.Enrollee.IsIdp,
                    encounter.Enrollee.OtherVulnerableCategory))
                .ToListAsync(cancellationToken);

            int totalEnrollees = enrollees.Count;
            int totalEncounters = encounters.Count;
            int uniqueEncounterEnrollees = encounters
                .Select(encounter => encounter.EnrolleeId)
                .Distinct()
                .Count();

            EncounterDemographicMatrixViewModel model = new()
            {
                ScopeLabel = string.IsNullOrWhiteSpace(scopeLabel) ? "All states" : scopeLabel,
                TotalEnrollees = totalEnrollees,
                UniqueEnrolleesWithEncounters = uniqueEncounterEnrollees,
                TotalEncounters = totalEncounters
            };

            model.Rows.AddRange(BuildRows(
                "Gender",
                new[] { "Male", "Female", "Other / Unspecified" },
                profile => NormalizeGender(profile.Gender),
                fact => NormalizeGender(fact.Gender),
                enrollees,
                encounters,
                totalEnrollees,
                totalEncounters));

            model.Rows.AddRange(BuildRows(
                "Age band",
                new[] { "Under 5", "5-17", "18-35", "36-59", "60+" },
                profile => AgeBand(profile.DateOfBirth, today),
                fact => AgeBand(fact.DateOfBirth, today),
                enrollees,
                encounters,
                totalEnrollees,
                totalEncounters));

            model.Rows.AddRange(BuildRows(
                "Vulnerability",
                new[] { "Pregnant women", "Children under 5", "Elderly (60+)", "PLWD", "IDP / Other", "Not flagged" },
                profile => VulnerabilityCategory(
                    profile.Gender,
                    profile.DateOfBirth,
                    profile.IsPregnant,
                    profile.HasDisability,
                    profile.IsIdp,
                    profile.OtherVulnerableCategory,
                    today),
                fact => VulnerabilityCategory(
                    fact.Gender,
                    fact.DateOfBirth,
                    fact.IsPregnant,
                    fact.HasDisability,
                    fact.IsIdp,
                    fact.OtherVulnerableCategory,
                    today),
                enrollees,
                encounters,
                totalEnrollees,
                totalEncounters));

            return model;
        }

        private static IEnumerable<EncounterDemographicMatrixRowViewModel> BuildRows(
            string dimension,
            IReadOnlyList<string> categories,
            Func<DemographicProfile, string> enrolleeCategorySelector,
            Func<EncounterDemographicFact, string> encounterCategorySelector,
            IReadOnlyList<DemographicProfile> enrollees,
            IReadOnlyList<EncounterDemographicFact> encounters,
            int totalEnrollees,
            int totalEncounters)
        {
            Dictionary<string, int> enrolleeCounts = enrollees
                .GroupBy(enrolleeCategorySelector, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            Dictionary<string, int> encounterCounts = encounters
                .GroupBy(encounterCategorySelector, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            Dictionary<string, int> uniqueEncounterEnrolleeCounts = encounters
                .GroupBy(encounterCategorySelector, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(encounter => encounter.EnrolleeId).Distinct().Count(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (string category in categories)
            {
                enrolleeCounts.TryGetValue(category, out int enrolleeCount);
                encounterCounts.TryGetValue(category, out int encounterCount);
                uniqueEncounterEnrolleeCounts.TryGetValue(category, out int uniqueEncounterEnrolleeCount);

                yield return new EncounterDemographicMatrixRowViewModel
                {
                    Dimension = dimension,
                    Category = category,
                    Enrollees = enrolleeCount,
                    EnrolleesWithEncounters = uniqueEncounterEnrolleeCount,
                    Encounters = encounterCount,
                    EnrolleeShare = Percentage(enrolleeCount, totalEnrollees),
                    EncounterShare = Percentage(encounterCount, totalEncounters),
                    EncountersPerThousandEnrollees = RatePerThousand(encounterCount, enrolleeCount)
                };
            }
        }

        private static string NormalizeGender(string? gender)
        {
            if (string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase)
                || string.Equals(gender, "M", StringComparison.OrdinalIgnoreCase))
            {
                return "Male";
            }

            if (string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase)
                || string.Equals(gender, "F", StringComparison.OrdinalIgnoreCase))
            {
                return "Female";
            }

            return "Other / Unspecified";
        }

        private static string AgeBand(DateTime dateOfBirth, DateTime today)
        {
            int age = CalculateAge(dateOfBirth, today);
            return age switch
            {
                < 5 => "Under 5",
                < 18 => "5-17",
                < 36 => "18-35",
                < 60 => "36-59",
                _ => "60+"
            };
        }

        private static string VulnerabilityCategory(
            string? gender,
            DateTime dateOfBirth,
            bool isPregnant,
            bool hasDisability,
            bool isIdp,
            string? otherVulnerableCategory,
            DateTime today)
        {
            if (isPregnant && NormalizeGender(gender) == "Female")
            {
                return "Pregnant women";
            }

            int age = CalculateAge(dateOfBirth, today);
            if (age < 5)
            {
                return "Children under 5";
            }

            if (age >= ElderlyAge)
            {
                return "Elderly (60+)";
            }

            if (hasDisability)
            {
                return "PLWD";
            }

            if (isIdp || !string.IsNullOrWhiteSpace(otherVulnerableCategory))
            {
                return "IDP / Other";
            }

            return "Not flagged";
        }

        private static int CalculateAge(DateTime dateOfBirth, DateTime today)
        {
            int age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }

            return Math.Max(0, age);
        }

        private static decimal Percentage(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 100m, 1)
                : 0m;
        }

        private static decimal RatePerThousand(int numerator, int denominator)
        {
            return denominator > 0
                ? Math.Round((decimal)numerator / denominator * 1000m, 1)
                : 0m;
        }

        private sealed record DemographicProfile(
            int EnrolleeId,
            string? Gender,
            DateTime DateOfBirth,
            bool IsPregnant,
            bool HasDisability,
            bool IsIdp,
            string? OtherVulnerableCategory);

        private sealed record EncounterDemographicFact(
            int EnrolleeId,
            string? Gender,
            DateTime DateOfBirth,
            bool IsPregnant,
            bool HasDisability,
            bool IsIdp,
            string? OtherVulnerableCategory);
    }
}
