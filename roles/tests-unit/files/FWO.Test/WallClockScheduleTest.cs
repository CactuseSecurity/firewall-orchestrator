using System.Globalization;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using Quartz;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that schedules keep their configured wall-clock time across the switches between
    /// summer and winter time (Europe/Berlin: 25.10.2026 and 28.03.2027).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class WallClockScheduleTest
    {
        private const string kFireTimeFormat = "yyyy-MM-dd HH:mm zzz";
        private static readonly TimeZoneInfo kBerlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        private static readonly TriggerKey kTriggerKey = new("TestTrigger");
        private static readonly JobKey kJobKey = new("TestJob");

        private static readonly List<string> kDailyAcrossAutumnSwitch =
        [
            "2026-10-24 06:00 +02:00",
            "2026-10-25 06:00 +01:00",
            "2026-10-26 06:00 +01:00"
        ];

        private static readonly List<string> kDailyAcrossSpringSwitch =
        [
            "2027-03-27 06:00 +01:00",
            "2027-03-28 06:00 +02:00",
            "2027-03-29 06:00 +02:00"
        ];

        private static readonly List<string> kRepeatedHourRunsOnce =
        [
            "2026-10-25 02:30 +02:00",
            "2026-10-26 02:30 +01:00"
        ];

        private static readonly List<string> kMissingHourRunsAfterGap =
        [
            "2027-03-27 02:30 +01:00",
            "2027-03-28 03:00 +02:00",
            "2027-03-29 02:30 +02:00"
        ];

        private static readonly List<string> kGapAnchoredOnPastRun =
        [
            "2027-03-28 03:00 +02:00",
            "2027-03-29 02:30 +02:00"
        ];

        private static readonly List<string> kTwoDaysAcrossSpringSwitch =
        [
            "2027-03-27 06:00 +01:00",
            "2027-03-29 06:00 +02:00",
            "2027-03-31 06:00 +02:00"
        ];

        private static readonly List<string> kTwelveHoursAcrossAutumnSwitch =
        [
            "2026-10-24 18:00 +02:00",
            "2026-10-25 06:00 +01:00",
            "2026-10-25 18:00 +01:00",
            "2026-10-26 06:00 +01:00"
        ];

        private static readonly List<string> kTwoHoursAcrossAutumnSwitch =
        [
            "2026-10-25 00:00 +02:00",
            "2026-10-25 02:00 +02:00",
            "2026-10-25 04:00 +01:00",
            "2026-10-25 06:00 +01:00"
        ];

        private static readonly List<string> kSixHoursAcrossSpringSwitch =
        [
            "2027-03-28 00:00 +01:00",
            "2027-03-28 06:00 +02:00",
            "2027-03-28 12:00 +02:00"
        ];

        [TestCase(24 * 60, WallClockScheduleKind.Days)]
        [TestCase(48 * 60, WallClockScheduleKind.Days)]
        [TestCase(12 * 60, WallClockScheduleKind.HoursOfDay)]
        [TestCase(6 * 60, WallClockScheduleKind.HoursOfDay)]
        [TestCase(2 * 60, WallClockScheduleKind.HoursOfDay)]
        [TestCase(60, WallClockScheduleKind.FixedInterval)]
        [TestCase(5 * 60, WallClockScheduleKind.FixedInterval)]
        [TestCase(36 * 60, WallClockScheduleKind.FixedInterval)]
        [TestCase(90, WallClockScheduleKind.FixedInterval)]
        [TestCase(15, WallClockScheduleKind.FixedInterval)]
        public void GetScheduleKind_MapsIntervalToScheduleKind(int intervalMinutes, WallClockScheduleKind expected)
        {
            Assert.That(WallClockSchedule.GetScheduleKind(TimeSpan.FromMinutes(intervalMinutes)), Is.EqualTo(expected));
        }

        [Test]
        public void GetScheduleKind_ThrowsOnNonPositiveInterval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WallClockSchedule.GetScheduleKind(TimeSpan.Zero));
        }

        [Test]
        public void ToInstant_RegularTime_UsesOffsetOfThatDate()
        {
            DateTimeOffset summer = WallClockSchedule.ToInstant(new DateTime(2026, 7, 1, 6, 0, 0), kBerlin);
            DateTimeOffset winter = WallClockSchedule.ToInstant(new DateTime(2026, 12, 1, 6, 0, 0), kBerlin);

            Assert.That(summer, Is.EqualTo(new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero)));
            Assert.That(winter, Is.EqualTo(new DateTimeOffset(2026, 12, 1, 5, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void ToInstant_TimeInSpringGap_MapsToEndOfGap()
        {
            DateTimeOffset instant = WallClockSchedule.ToInstant(new DateTime(2027, 3, 28, 2, 30, 0), kBerlin);

            Assert.That(instant, Is.EqualTo(new DateTimeOffset(2027, 3, 28, 3, 0, 0, TimeSpan.FromHours(2))));
        }

        [Test]
        public void ToInstant_RepeatedAutumnTime_MapsToFirstOccurrence()
        {
            DateTimeOffset instant = WallClockSchedule.ToInstant(new DateTime(2026, 10, 25, 2, 30, 0), kBerlin);

            Assert.That(instant, Is.EqualTo(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2))));
        }

        [Test]
        public void DayTrigger_CreatedInSummer_KeepsWallClockTimeAfterAutumnSwitch()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 23, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 6, 0, 0), TimeSpan.FromHours(24), now);

            Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kDailyAcrossAutumnSwitch));
        }

        [Test]
        public void DayTrigger_CreatedInWinter_KeepsWallClockTimeAfterSpringSwitch()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 26, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 11, 1, 6, 0, 0), TimeSpan.FromDays(1), now);

            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kDailyAcrossSpringSwitch));
        }

        [Test]
        public void DayTrigger_StartInRepeatedAutumnHour_RunsOnlyOnce()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 2, 30, 0), TimeSpan.FromDays(1), now);

            Assert.That(FireTimes(trigger, now, 2), Is.EqualTo(kRepeatedHourRunsOnce));
        }

        [Test]
        public void DayTrigger_StartInMissingSpringHour_RunsAtNextValidTime()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 26, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2027, 1, 1, 2, 30, 0), TimeSpan.FromDays(1), now);

            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kMissingHourRunsAfterGap));
        }

        [Test]
        public void DayTrigger_NextRunInSpringGap_AnchorsOnPreviousRun()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 27, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2027, 1, 1, 2, 30, 0), TimeSpan.FromDays(1), now);

            Assert.That(trigger.StartTimeUtc, Is.EqualTo(LocalInstant(2027, 3, 27, 2, 30)));
            Assert.That(FireTimes(trigger, now, 2), Is.EqualTo(kGapAnchoredOnPastRun));
        }

        /// <summary>
        /// Rescheduling shortly after a run must retain the run in the spring gap without an immediate misfire.
        /// </summary>
        [TestCase(1, false)]
        [TestCase(60, false)]
        [TestCase(119, false)]
        [TestCase(120, false)]
        [TestCase(60, true)]
        public void DayTrigger_RescheduledAfterRun_KeepsSpringGapRun(int secondsAfterRun, bool justStarted)
        {
            DateTimeOffset now = LocalInstant(2027, 3, 27, 2, 30).AddSeconds(secondsAfterRun);
            DateTime start = justStarted ? new DateTime(2027, 3, 27, 2, 30, 0) : new DateTime(2027, 1, 1, 2, 30, 0);

            ITrigger trigger = CreateTrigger(start, TimeSpan.FromDays(1), now);

            Assert.Multiple(() =>
            {
                Assert.That(trigger.StartTimeUtc, Is.LessThanOrEqualTo(now.AddMinutes(-2)));
                Assert.That(FireTimes(trigger, now, 2), Is.EqualTo(kGapAnchoredOnPastRun));
            });
        }

        [Test]
        public void DayTrigger_FirstRunInSpringGap_SkipsNonExistentRun()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 1, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2027, 3, 28, 2, 30, 0), TimeSpan.FromDays(1), now);

            Assert.That(trigger.StartTimeUtc, Is.EqualTo(LocalInstant(2027, 3, 29, 2, 30)));
        }

        [Test]
        public void DayTrigger_MultipleDays_KeepsWallClockTimeAfterSpringSwitch()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 26, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2027, 3, 1, 6, 0, 0), TimeSpan.FromDays(2), now);

            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kTwoDaysAcrossSpringSwitch));
        }

        [Test]
        public void DayTrigger_FutureStart_StartsAtConfiguredTime()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 1, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 11, 2, 6, 0, 0), TimeSpan.FromDays(1), now);

            Assert.That(trigger.StartTimeUtc, Is.EqualTo(LocalInstant(2026, 11, 2, 6, 0)));
        }

        [Test]
        public void DayTrigger_UtcStart_IsMovedOntoWallClock()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 23, 12, 0);
            DateTime utcStart = new(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc);

            ITrigger trigger = CreateTrigger(utcStart, TimeSpan.FromDays(1), now);

            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kDailyAcrossAutumnSwitch));
        }

        [Test]
        public void HoursOfDayCronExpression_ListsAllSlotsOfTheDay()
        {
            string expression = WallClockSchedule.BuildHoursOfDayCronExpression(new DateTime(2026, 1, 1, 18, 15, 5), TimeSpan.FromHours(6));

            Assert.That(expression, Is.EqualTo("5 15 0,6,12,18 * * ?"));
        }

        [Test]
        public void HoursOfDayTrigger_KeepsWallClockSlotsAfterAutumnSwitch()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 18, 0, 0), TimeSpan.FromHours(12), now);

            Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
            Assert.That(FireTimes(trigger, now, 4), Is.EqualTo(kTwelveHoursAcrossAutumnSwitch));
        }

        [Test]
        public void HoursOfDayTrigger_RepeatedAutumnHour_RunsOnlyOnce()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 23, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 0, 0, 0), TimeSpan.FromHours(2), now);

            Assert.That(FireTimes(trigger, now, 4), Is.EqualTo(kTwoHoursAcrossAutumnSwitch));
        }

        [Test]
        public void HoursOfDayTrigger_KeepsWallClockSlotsAfterSpringSwitch()
        {
            DateTimeOffset now = LocalInstant(2027, 3, 27, 23, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2027, 1, 1, 6, 0, 0), TimeSpan.FromMinutes(6 * 60), now);

            Assert.That(FireTimes(trigger, now, 3), Is.EqualTo(kSixHoursAcrossSpringSwitch));
        }

        [Test]
        public void HoursOfDayTrigger_FutureStart_DoesNotRunBeforeConfiguredStart()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 1, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 10, 3, 6, 0, 0), TimeSpan.FromHours(12), now);

            Assert.That(trigger.GetFireTimeAfter(now), Is.EqualTo(LocalInstant(2026, 10, 3, 6, 0)));
        }

        [Test]
        public void FixedIntervalTrigger_PastStart_StartsAtNextStep()
        {
            DateTimeOffset now = LocalInstant(2026, 1, 1, 12, 2, 30);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 12, 0, 0), TimeSpan.FromMinutes(1), now);

            Assert.That(trigger, Is.InstanceOf<ISimpleTrigger>());
            Assert.That(trigger.StartTimeUtc, Is.EqualTo(LocalInstant(2026, 1, 1, 12, 3)));
        }

        [Test]
        public void FixedIntervalTrigger_FutureStart_IsKept()
        {
            DateTimeOffset now = LocalInstant(2026, 1, 1, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 12, 10, 0), TimeSpan.FromMinutes(5), now);

            Assert.That(trigger.StartTimeUtc, Is.EqualTo(LocalInstant(2026, 1, 1, 12, 10)));
        }

        [Test]
        public void FixedIntervalTrigger_RepeatsInAbsoluteTime()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 22, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 10, 24, 23, 0, 0), TimeSpan.FromHours(5), now);

            DateTimeOffset? secondRun = trigger.GetFireTimeAfter(trigger.StartTimeUtc);

            Assert.That(secondRun - trigger.StartTimeUtc, Is.EqualTo(TimeSpan.FromHours(5)));
        }

        [Test]
        public void FixedIntervalTrigger_MinimalStart_DoesNotOverflow()
        {
            DateTimeOffset now = LocalInstant(2026, 1, 1, 12, 0, 3);
            ITrigger trigger = CreateTrigger(DateTime.MinValue, TimeSpan.FromSeconds(10), now);

            Assert.That(trigger.StartTimeUtc, Is.GreaterThanOrEqualTo(now));
            Assert.That(trigger.StartTimeUtc, Is.LessThan(now.AddSeconds(10)));
        }

        [Test]
        public void FormatNextFireTime_IncludesUtcOffset()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 12, 0);
            ITrigger trigger = CreateTrigger(new DateTime(2026, 1, 1, 6, 0, 0), TimeSpan.FromDays(1), now);

            Assert.That(WallClockSchedule.FormatNextFireTime(trigger, now, kBerlin), Is.EqualTo("2026-10-25 06:00:00 +01:00"));
        }

        [Test]
        public void FormatNextFireTime_TriggerWithoutFurtherRuns_ReturnsNever()
        {
            DateTimeOffset now = LocalInstant(2026, 10, 24, 12, 0);
            ITrigger trigger = TriggerBuilder.Create()
                .StartAt(now.AddHours(-1))
                .WithSimpleSchedule(schedule => schedule.WithRepeatCount(0))
                .Build();

            Assert.That(WallClockSchedule.FormatNextFireTime(trigger, now, kBerlin), Is.EqualTo("never"));
        }

        private static ITrigger CreateTrigger(DateTime configuredStart, TimeSpan interval, DateTimeOffset now)
        {
            return WallClockSchedule.CreateTrigger(kTriggerKey, kJobKey, configuredStart, interval, now, kBerlin);
        }

        private static DateTimeOffset LocalInstant(int year, int month, int day, int hour, int minute, int second = 0)
        {
            return WallClockSchedule.ToInstant(new DateTime(year, month, day, hour, minute, second), kBerlin);
        }

        /// <summary>
        /// Lists the next fire times of a trigger as Quartz computes them, formatted as Berlin wall clock with offset.
        /// </summary>
        private static List<string> FireTimes(ITrigger trigger, DateTimeOffset now, int count)
        {
            List<string> fireTimes = [];
            DateTimeOffset? fireTime = trigger.GetFireTimeAfter(now);
            while (fireTime != null && fireTimes.Count < count)
            {
                DateTimeOffset localFireTime = TimeZoneInfo.ConvertTime(fireTime.Value, kBerlin);
                fireTimes.Add(localFireTime.ToString(kFireTimeFormat, CultureInfo.InvariantCulture));
                fireTime = trigger.GetFireTimeAfter(fireTime);
            }
            return fireTimes;
        }
    }
}
