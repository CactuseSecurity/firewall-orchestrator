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

        /// <summary>
        /// Title for the log data. All import files are expected to name the same period, so the period
        /// stored by the last import describes the whole table. Without a stored period, e.g. before
        /// the first import, the plain title is used.
        /// </summary>
        public static string Build(LogDataImportPeriod? period, UserConfig userConfig)
        {
            if (period is null || period.LogTimeRangeInSeconds <= 0)
            {
                return userConfig.GetText("log_data");
            }
            return userConfig.GetText("log_data_aggregated")
                .Replace(Placeholder.TIME_INTERVAL, FormatTimeRange(period.LogTimeRangeInSeconds, userConfig))
                .Replace(Placeholder.DATE, FormatImportTime(period.ImportTime, userConfig));
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
        /// </summary>
        public static string FormatImportTime(DateTimeOffset importTime, UserConfig userConfig)
        {
            return importTime.ToLocalTime().DateTime.ToString(kImportTimeFormat, userConfig.GetUserCulture());
        }
    }
}
