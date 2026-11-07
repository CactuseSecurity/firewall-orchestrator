using FWO.Data.Enums;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Converts the configured rule hit import interval into Quartz scheduling values.
    /// </summary>
    public static class RuleHitImportSchedule
    {
        /// <summary>
        /// Creates a scheduling interval from the configured value and unit.
        /// </summary>
        /// <param name="intervalValue">Configured interval value; negative values count as zero.</param>
        /// <param name="intervalUnit">Configured interval unit.</param>
        /// <returns>The interval between two rule hit import requests.</returns>
        public static TimeSpan GetInterval(int intervalValue, RuleHitImportIntervalUnit intervalUnit)
        {
            int safeIntervalValue = Math.Max(0, intervalValue);
            return intervalUnit switch
            {
                RuleHitImportIntervalUnit.Minutes => TimeSpan.FromMinutes(safeIntervalValue),
                RuleHitImportIntervalUnit.Hours => TimeSpan.FromHours(safeIntervalValue),
                RuleHitImportIntervalUnit.Days => TimeSpan.FromDays(safeIntervalValue),
                _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unsupported rule hit import interval unit.")
            };
        }

        /// <summary>
        /// Gets the unit suffix used in scheduler log messages.
        /// </summary>
        /// <param name="intervalUnit">Configured interval unit.</param>
        /// <returns>The unit suffix.</returns>
        public static string GetIntervalLogSuffix(RuleHitImportIntervalUnit intervalUnit)
        {
            return intervalUnit switch
            {
                RuleHitImportIntervalUnit.Minutes => "m",
                RuleHitImportIntervalUnit.Hours => "h",
                RuleHitImportIntervalUnit.Days => "d",
                _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unsupported rule hit import interval unit.")
            };
        }
    }
}
