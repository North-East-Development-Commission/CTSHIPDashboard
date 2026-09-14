using System;

namespace CTSHIPDashboard.Helpers
{
    public static class CtshipClock
    {
        private static readonly Lazy<TimeZoneInfo> WestAfricaTimeZone = new(ResolveWestAfricaTimeZone);

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, WestAfricaTimeZone.Value);

        public static DateTime Today => Now.Date;

        public static DateTime ToLocal(DateTime value)
        {
            DateTime utcValue = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utcValue, WestAfricaTimeZone.Value);
        }

        public static DateTime? ToLocal(DateTime? value) => value.HasValue ? ToLocal(value.Value) : null;

        private static TimeZoneInfo ResolveWestAfricaTimeZone()
        {
            foreach (string id in new[] { "W. Central Africa Standard Time", "Africa/Lagos" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return TimeZoneInfo.CreateCustomTimeZone("West Africa Time", TimeSpan.FromHours(1), "West Africa Time", "West Africa Time");
        }
    }
}