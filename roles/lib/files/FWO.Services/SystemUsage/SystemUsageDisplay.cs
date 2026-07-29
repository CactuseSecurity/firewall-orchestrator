using System.Globalization;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Formatting helpers for the system usage values.
    /// </summary>
    public static class SystemUsageDisplay
    {
        private static readonly List<string> kByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];
        private const double kUnitStep = 1024.0;

        /// <summary>
        /// Formats a byte count with a binary unit prefix, e.g. "1.5 GB".
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <param name="decimals">Number of decimal places.</param>
        /// <returns>The formatted value.</returns>
        public static string FormatBytes(long bytes, int decimals = 1)
        {
            if (bytes < 0)
            {
                return $"0 {kByteUnits[0]}";
            }

            double value = bytes;
            int unitIndex = 0;
            while (value >= kUnitStep && unitIndex < kByteUnits.Count - 1)
            {
                value /= kUnitStep;
                unitIndex++;
            }
            // full bytes never need decimal places
            string formatted = unitIndex == 0
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString($"0.{new string('#', Math.Max(0, decimals))}", CultureInfo.InvariantCulture);
            return $"{formatted} {kByteUnits[unitIndex]}";
        }

        /// <summary>
        /// Formats a percentage value, e.g. "42.3 %".
        /// </summary>
        /// <param name="percent">Value in percent.</param>
        /// <returns>The formatted value.</returns>
        public static string FormatPercent(double percent)
        {
            return $"{Math.Clamp(percent, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)} %";
        }

        /// <summary>
        /// Formats the three load averages of a snapshot, e.g. "0.50 / 1.50 / 2.50".
        /// </summary>
        /// <param name="snapshot">Snapshot holding the load averages.</param>
        /// <returns>The formatted value.</returns>
        public static string FormatLoadAverage(SystemUsageSnapshot snapshot)
        {
            return string.Join(" / ", snapshot.LoadAverage1.ToString("0.00", CultureInfo.InvariantCulture),
                snapshot.LoadAverage5.ToString("0.00", CultureInfo.InvariantCulture),
                snapshot.LoadAverage15.ToString("0.00", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Formats a duration as days, hours and minutes, e.g. "2d 03:14".
        /// </summary>
        /// <param name="duration">Duration to format.</param>
        /// <returns>The formatted value.</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }
            return duration.Days > 0
                ? $"{duration.Days}d {duration.Hours:00}:{duration.Minutes:00}"
                : $"{duration.Hours:00}:{duration.Minutes:00}";
        }
    }
}
