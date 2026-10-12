using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Data.Extensions;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Builds the title of the log data table, which names the period the displayed log counts were
    /// aggregated over and when they were imported. Kept out of the component so it can be tested
    /// without rendering.
    /// </summary>
    public static class LogDataTitle
    {
        // short date and time pattern of the user culture, e.g. 05.10.2026 14:30 or 10/5/2026 2:30 PM
        private const string kImportTimeFormat = "g";
        private const string kImportDateFormat = "d";

        /// <summary>
        /// Title for log rows sharing one known aggregation period and import time.
        /// Without a common period, the plain title is used and the rows show their own timing.
        /// </summary>
        public static string Build(LogDataImportPeriod? period, UserConfig userConfig)
        {
            if (period is null || period.LogTimeRangeInSeconds <= 0)
            {
                return userConfig.GetText("log_data");
            }
            return userConfig.GetText("log_data_aggregated")
                .Replace(Placeholder.TIME_INTERVAL, FormatTimeRange(period.LogTimeRangeInSeconds, userConfig))
                .Replace(Placeholder.DATE, FormatImportTime(period.ImportTime, userConfig, userConfig.ShowLogImportTimeInHeading));
        }

        /// <summary>
        /// Uses only the displayed rows. A common title is safe only when every row has the
        /// same known aggregation period and import time.
        /// </summary>
        public static LogDataImportPeriod? GetCommonPeriod(IEnumerable<FirewallLogEntry> entries)
        {
            List<FirewallLogEntry> rows = entries.ToList();
            FirewallLogEntry? first = rows.FirstOrDefault();
            if (first?.ImportTime is null || first.LogTimeRangeInSeconds is not > 0
                || rows.Any(row => row.ImportTime != first.ImportTime
                    || row.LogTimeRangeInSeconds != first.LogTimeRangeInSeconds))
            {
                return null;
            }
            return new LogDataImportPeriod
            {
                ImportTime = first.ImportTime.Value,
                LogTimeRangeInSeconds = first.LogTimeRangeInSeconds.Value
            };
        }

        /// <summary>
        /// Human readable form of a period in the user language, e.g. "1 Week(s)" or "1 Day(s) 12 Hour(s)".
        /// </summary>
        public static string FormatTimeRange(int seconds, UserConfig userConfig)
        {
            return string.Join(" ", LogTimeRange.Split(seconds)
                .Select(part => $"{part.Value} {part.Unit.ToString(userConfig)}"));
        }

        /// <summary>
        /// Import time in the timezone of the server the UI runs on, as every timestamp of the
        /// application is displayed, and in the date format of the user language.
        /// The aggregation heading can omit the time; import time columns retain it.
        /// </summary>
        public static string FormatImportTime(DateTimeOffset importTime, UserConfig userConfig, bool includeTime = true)
        {
            return importTime.ToLocalTime().DateTime.ToString(includeTime ? kImportTimeFormat : kImportDateFormat, userConfig.GetUserCulture());
        }
    }
}
