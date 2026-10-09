using FWO.Basics;
using FWO.Data.Enums;

namespace FWO.Data
{
    /// <summary>
    /// Converts the period log counts were aggregated over between seconds, as stored and imported,
    /// and the units it is entered and displayed in.
    /// </summary>
    public static class LogTimeRange
    {
        private static readonly List<LogTimeRangeUnit> kUnitsLargestFirst =
            [LogTimeRangeUnit.Weeks, LogTimeRangeUnit.Days, LogTimeRangeUnit.Hours, LogTimeRangeUnit.Minutes, LogTimeRangeUnit.Seconds];

        /// <summary>
        /// Seconds one unit stands for.
        /// </summary>
        public static int SecondsPerUnit(LogTimeRangeUnit unit)
        {
            return unit switch
            {
                LogTimeRangeUnit.Weeks => GlobalConst.kDaysPerWeek * (int)TimeSpan.SecondsPerDay,
                LogTimeRangeUnit.Days => (int)TimeSpan.SecondsPerDay,
                LogTimeRangeUnit.Hours => (int)TimeSpan.SecondsPerHour,
                LogTimeRangeUnit.Minutes => (int)TimeSpan.SecondsPerMinute,
                _ => 1
            };
        }

        /// <summary>
        /// Converts a value entered in the given unit into seconds. A value which does not fit into
        /// the stored seconds is capped instead of overflowing into a negative period.
        /// </summary>
        public static int ToSeconds(int value, LogTimeRangeUnit unit)
        {
            return (int)Math.Clamp((long)value * SecondsPerUnit(unit), int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// Expresses seconds in the largest unit which represents them exactly, so 604800 seconds are
        /// edited as 1 week, 172800 seconds as 2 days and 90 seconds as 90 seconds.
        /// </summary>
        public static (int Value, LogTimeRangeUnit Unit) ToLargestExactUnit(int seconds)
        {
            if (seconds <= 0)
            {
                return (seconds, LogTimeRangeUnit.Seconds);
            }
            LogTimeRangeUnit unit = kUnitsLargestFirst.First(candidate => seconds % SecondsPerUnit(candidate) == 0);
            return (seconds / SecondsPerUnit(unit), unit);
        }

        /// <summary>
        /// Splits seconds into the non-zero amounts of weeks, days, hours, minutes and seconds they
        /// consist of, largest unit first, e.g. 129600 seconds into 1 day and 12 hours.
        /// </summary>
        /// <returns>The parts, empty for a period which is not positive.</returns>
        public static List<(int Value, LogTimeRangeUnit Unit)> Split(int seconds)
        {
            List<(int Value, LogTimeRangeUnit Unit)> parts = [];
            int remainingSeconds = seconds;
            foreach (LogTimeRangeUnit unit in kUnitsLargestFirst)
            {
                int value = remainingSeconds / SecondsPerUnit(unit);
                if (value > 0)
                {
                    parts.Add((value, unit));
                    remainingSeconds -= value * SecondsPerUnit(unit);
                }
            }
            return parts;
        }
    }
}
