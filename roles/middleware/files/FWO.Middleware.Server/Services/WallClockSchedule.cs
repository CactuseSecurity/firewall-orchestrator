using System.Globalization;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// How a recurring schedule is mapped onto a Quartz trigger.
    /// </summary>
    public enum WallClockScheduleKind
    {
        /// <summary>
        /// Interval of whole days: runs at the configured wall-clock time of day.
        /// </summary>
        Days,

        /// <summary>
        /// Whole-hour interval above one hour that divides a day: runs at fixed wall-clock hours of each day.
        /// </summary>
        HoursOfDay,

        /// <summary>
        /// Any other interval: repeats in absolute time and makes no wall-clock promise.
        /// </summary>
        FixedInterval
    }

    /// <summary>
    /// Builds Quartz triggers that keep a configured wall-clock start time across daylight saving time switches.
    /// </summary>
    /// <remarks>
    /// A configured start time is a timezone-naive wall clock of the installation. Converting it to an instant
    /// once and repeating it in absolute time would shift the job by the DST difference after every switch.
    /// Day and hour-of-day schedules are therefore evaluated in the given time zone. A start time that does not
    /// exist on a spring switch day runs at the first valid time after the gap; a start time that occurs twice
    /// on an autumn switch day runs only once, at its first occurrence. A future first run of a day schedule
    /// inside the spring gap is skipped so later runs retain the configured time of day.
    /// </remarks>
    public static class WallClockSchedule
    {
        /// <summary>
        /// Trigger data key retaining the configured interval for the scheduler monitor.
        /// </summary>
        internal const string kIntervalTicks = "WallClockIntervalTicks";

        private const int kHoursPerDay = 24;
        private const int kMaxGapMinutes = kHoursPerDay * 60;
        private const string kLogTimeFormat = "yyyy-MM-dd HH:mm:ss zzz";
        private static readonly TimeSpan kOneDay = TimeSpan.FromDays(1);
        private static readonly TimeSpan kOneHour = TimeSpan.FromHours(1);
        private static readonly TimeSpan kOneMinute = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Distance to the current time a past trigger start needs, so Quartz treats it as misfired
        /// (default misfire threshold: one minute) instead of firing it immediately.
        /// </summary>
        private static readonly TimeSpan kMisfireSafetyMargin = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Determines how a schedule with the given interval is mapped onto a trigger.
        /// </summary>
        /// <param name="interval">Repeat interval.</param>
        /// <returns>The schedule kind.</returns>
        public static WallClockScheduleKind GetScheduleKind(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            if (interval.Ticks % kOneDay.Ticks == 0)
            {
                return WallClockScheduleKind.Days;
            }

            bool isWholeHours = interval.Ticks % kOneHour.Ticks == 0;
            bool dividesDay = kOneDay.Ticks % interval.Ticks == 0;
            if (interval > kOneHour && isWholeHours && dividesDay)
            {
                return WallClockScheduleKind.HoursOfDay;
            }

            return WallClockScheduleKind.FixedInterval;
        }

        /// <summary>
        /// Creates a recurring trigger for a configured wall-clock start time and interval.
        /// </summary>
        /// <param name="triggerKey">Key of the trigger.</param>
        /// <param name="jobKey">Key of the job the trigger fires.</param>
        /// <param name="configuredStart">Configured start time (wall clock of the installation).</param>
        /// <param name="interval">Repeat interval.</param>
        /// <param name="now">Current time.</param>
        /// <param name="zone">Time zone the wall clock refers to.</param>
        /// <returns>The trigger, not yet scheduled.</returns>
        public static ITrigger CreateTrigger(TriggerKey triggerKey, JobKey jobKey, DateTime configuredStart, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            TriggerBuilder<IJob> builder = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey);
            DateTime wallClockStart = ToWallClock(configuredStart, zone);

            return GetScheduleKind(interval) switch
            {
                WallClockScheduleKind.Days => BuildDayTrigger(builder, wallClockStart, interval, now, zone),
                WallClockScheduleKind.HoursOfDay => BuildHoursOfDayTrigger(builder, wallClockStart, interval, now, zone),
                _ => BuildFixedIntervalTrigger(builder, wallClockStart, interval, now, zone)
            };
        }

        /// <summary>
        /// Converts a wall-clock time to an instant in the given time zone.
        /// </summary>
        /// <param name="wallClock">Wall-clock time; its kind is ignored.</param>
        /// <param name="zone">Time zone the wall clock refers to.</param>
        /// <returns>
        /// The instant. A time inside a spring gap maps to the first valid time after the gap,
        /// a time that occurs twice maps to its first occurrence.
        /// </returns>
        public static DateTimeOffset ToInstant(DateTime wallClock, TimeZoneInfo zone)
        {
            DateTime local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local))
            {
                local = GetEndOfGap(local, zone);
            }

            if (zone.IsAmbiguousTime(local))
            {
                // The clock is set back, so the first occurrence carries the larger offset.
                TimeSpan firstOccurrenceOffset = zone.GetAmbiguousTimeOffsets(local).Max();
                return new DateTimeOffset(local, firstOccurrenceOffset);
            }

            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        /// <summary>
        /// Formats the first fire time of a trigger after the given time, including its UTC offset.
        /// </summary>
        /// <param name="trigger">The trigger.</param>
        /// <param name="now">Current time.</param>
        /// <param name="zone">Time zone used for display.</param>
        /// <returns>The formatted fire time, or "never".</returns>
        public static string FormatNextFireTime(ITrigger trigger, DateTimeOffset now, TimeZoneInfo zone)
        {
            DateTimeOffset? nextFireTime = trigger.GetFireTimeAfter(now.AddTicks(-1));
            if (nextFireTime == null)
            {
                return "never";
            }

            DateTimeOffset localFireTime = TimeZoneInfo.ConvertTime(nextFireTime.Value, zone);
            return localFireTime.ToString(kLogTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds a calendar trigger preserving the configured time of day across DST changes.
        /// </summary>
        private static ITrigger BuildDayTrigger(TriggerBuilder<IJob> builder, DateTime start, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            DateTime nextOccurrence = GetNextWallClockOccurrence(start, interval, now, zone);
            DateTime anchor = GetDayTriggerAnchor(start, nextOccurrence, interval, now, zone);
            int intervalInDays = (int)(interval.Ticks / kOneDay.Ticks);

            return builder
                .StartAt(ToInstant(anchor, zone))
                .WithCalendarIntervalSchedule(schedule => schedule
                    .WithInterval(intervalInDays, IntervalUnit.Day)
                    .InTimeZone(zone)
                    .PreserveHourOfDayAcrossDaylightSavings(true)
                    .WithMisfireInstruction(CalendarIntervalTriggerMisfireInstruction.DoNothing))
                .Build();
        }

        /// <summary>
        /// Chooses the trigger start of a day schedule. Quartz takes the time of day of all later runs from the
        /// trigger start, so the start itself must exist on the wall clock.
        /// </summary>
        private static DateTime GetDayTriggerAnchor(DateTime start, DateTime nextOccurrence, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            if (!zone.IsInvalidTime(nextOccurrence))
            {
                return nextOccurrence;
            }

            if (ToInstant(start, zone) > now)
            {
                // A future first run cannot be anchored in the past without starting before the configured date.
                return nextOccurrence + interval;
            }

            // Anchor on a past run so DoNothing advances to the end of the gap. If the last run is too
            // recent, one additional day interval puts the anchor safely beyond the misfire threshold.
            DateTime previousOccurrence = nextOccurrence - interval;
            return ToInstant(previousOccurrence, zone) <= now - kMisfireSafetyMargin
                ? previousOccurrence
                : previousOccurrence - interval;
        }

        /// <summary>
        /// Builds a cron trigger firing at fixed wall-clock slots throughout each day.
        /// </summary>
        private static ITrigger BuildHoursOfDayTrigger(TriggerBuilder<IJob> builder, DateTime start, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            string cronExpression = BuildHoursOfDayCronExpression(start, interval);
            DateTime nowWallClock = TimeZoneInfo.ConvertTime(now, zone).DateTime;
            DateTimeOffset startAt = start > nowWallClock ? ToInstant(start, zone) : now;

            return builder
                .StartAt(startAt)
                .UsingJobData(kIntervalTicks, interval.Ticks)
                .WithCronSchedule(cronExpression, schedule => schedule
                    .InTimeZone(zone)
                    .WithMisfireInstruction(CronTriggerMisfireInstruction.DoNothing))
                .Build();
        }

        /// <summary>
        /// Builds a cron expression firing at the configured minute and second in every slot of the day.
        /// The hours are listed explicitly: unlike a step expression (e.g. 0/2), an explicit list makes Quartz
        /// fire only once in the hour that occurs twice on the autumn switch day.
        /// </summary>
        /// <param name="start">Configured start time (wall clock).</param>
        /// <param name="interval">Whole-hour interval that divides a day.</param>
        /// <returns>The cron expression.</returns>
        public static string BuildHoursOfDayCronExpression(DateTime start, TimeSpan interval)
        {
            int hourStep = (int)(interval.Ticks / kOneHour.Ticks);
            int firstHour = start.Hour % hourStep;
            int slotsPerDay = kHoursPerDay / hourStep;
            List<int> hours = [.. Enumerable.Range(0, slotsPerDay).Select(slot => firstHour + slot * hourStep)];
            string hourList = string.Join(",", hours);
            return $"{start.Second} {start.Minute} {hourList} * * ?";
        }

        /// <summary>
        /// Builds a trigger repeating at fixed intervals in absolute time.
        /// </summary>
        private static ITrigger BuildFixedIntervalTrigger(TriggerBuilder<IJob> builder, DateTime start, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            DateTime nowWallClock = TimeZoneInfo.ConvertTime(now, zone).DateTime;
            DateTime candidate = JumpCloseTo(start, interval, nowWallClock);
            DateTimeOffset startAt = AdvanceToNotBefore(ToInstant(candidate, zone), interval, now);

            return builder
                .StartAt(startAt)
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(interval)
                    .RepeatForever())
                .Build();
        }

        /// <summary>
        /// Gets the first wall-clock run at or after the current time, stepping in wall-clock time.
        /// </summary>
        private static DateTime GetNextWallClockOccurrence(DateTime start, TimeSpan interval, DateTimeOffset now, TimeZoneInfo zone)
        {
            DateTime nowWallClock = TimeZoneInfo.ConvertTime(now, zone).DateTime;
            DateTime candidate = JumpCloseTo(start, interval, nowWallClock);
            // The jump lands at most one step before the current time.
            while (ToInstant(candidate, zone) < now)
            {
                candidate = candidate.Add(interval);
            }
            return candidate;
        }

        /// <summary>
        /// Moves a past start forward by whole intervals to the last wall-clock step not after the reference time.
        /// </summary>
        private static DateTime JumpCloseTo(DateTime start, TimeSpan interval, DateTime reference)
        {
            if (start >= reference)
            {
                return start;
            }

            long steps = (reference - start).Ticks / interval.Ticks;
            return start.AddTicks(steps * interval.Ticks);
        }

        /// <summary>
        /// Moves an instant forward by whole intervals until it is not before the reference time.
        /// </summary>
        private static DateTimeOffset AdvanceToNotBefore(DateTimeOffset instant, TimeSpan interval, DateTimeOffset reference)
        {
            if (instant >= reference)
            {
                return instant;
            }

            long missingTicks = (reference - instant).Ticks;
            long steps = (missingTicks + interval.Ticks - 1) / interval.Ticks;
            return instant.AddTicks(steps * interval.Ticks);
        }

        /// <summary>
        /// Finds the first valid minute after a spring gap, probing at most one day.
        /// </summary>
        private static DateTime GetEndOfGap(DateTime local, TimeZoneInfo zone)
        {
            DateTime probe = new(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);
            for (int minute = 0; minute < kMaxGapMinutes && zone.IsInvalidTime(probe); minute++)
            {
                probe = probe.Add(kOneMinute);
            }
            return probe;
        }

        /// <summary>
        /// Reduces a configured start time to the wall clock of the zone. Configuration values carry no offset;
        /// an explicit UTC value is moved onto the wall clock.
        /// </summary>
        private static DateTime ToWallClock(DateTime configuredStart, TimeZoneInfo zone)
        {
            DateTime wallClock = configuredStart.Kind == DateTimeKind.Utc
                ? TimeZoneInfo.ConvertTimeFromUtc(configuredStart, zone)
                : configuredStart;
            return DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        }
    }
}
