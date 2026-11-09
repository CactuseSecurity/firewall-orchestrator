using FWO.Data.Enums;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Converts the configured log data import interval into Quartz scheduling values.
    /// </summary>
    public static class LogDataImportSchedule
    {
        /// <summary>
        /// Creates a scheduling interval from the configured value and unit.
        /// </summary>
        public static TimeSpan GetInterval(int intervalValue, LogDataImportIntervalUnit intervalUnit)
        {
            int safeIntervalValue = Math.Max(0, intervalValue);
            return intervalUnit switch
            {
                LogDataImportIntervalUnit.Seconds => TimeSpan.FromSeconds(safeIntervalValue),
                LogDataImportIntervalUnit.Minutes => TimeSpan.FromMinutes(safeIntervalValue),
                LogDataImportIntervalUnit.Hours => TimeSpan.FromHours(safeIntervalValue),
                _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unsupported log data import interval unit.")
            };
        }

        /// <summary>
        /// Gets the unit suffix used in scheduler log messages.
        /// </summary>
        public static string GetIntervalLogSuffix(LogDataImportIntervalUnit intervalUnit)
        {
            return intervalUnit switch
            {
                LogDataImportIntervalUnit.Seconds => "s",
                LogDataImportIntervalUnit.Minutes => "m",
                LogDataImportIntervalUnit.Hours => "h",
                _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unsupported log data import interval unit.")
            };
        }
    }
}
