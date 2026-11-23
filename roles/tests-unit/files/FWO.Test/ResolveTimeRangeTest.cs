using System.Globalization;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ResolveTimeRangeTest
    {
        private const string kDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        // Year, month and week boundaries are resolved relative to the reference time, so the cases
        // around December/January and month ends can be pinned independent of the current date.
        [TestCase("this year", "2026-12-31 23:59:59", "2026-01-01", "2027-01-01")]
        [TestCase("last year", "2026-01-01 00:00:00", "2025-01-01", "2026-01-01")]
        [TestCase("this month", "2026-12-15 10:30:00", "2026-12-01", "2027-01-01")]
        [TestCase("last month", "2026-01-15 10:30:00", "2025-12-01", "2026-01-01")]
        [TestCase("last month", "2026-03-31 10:30:00", "2026-02-01", "2026-03-01")]
        [TestCase("this week", "2026-12-15 10:30:00", "2026-12-13", "2026-12-16")]
        [TestCase("this week", "2026-11-22 10:30:00", "2026-11-22", "2026-11-23")]
        [TestCase("last week", "2026-01-01 10:30:00", "2025-12-21", "2025-12-28")]
        [TestCase("today", "2026-12-31 23:59:59", "2026-12-31", "2027-01-01")]
        [TestCase("yesterday", "2026-01-01 00:00:00", "2025-12-31", "2026-01-01")]
        public void Shortcut_IsResolvedAgainstReferenceTime(string shortcut, string referenceTime, string expectedStart, string expectedStop)
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Shortcut, TimeRangeShortcut = shortcut };

            (string start, string stop) = DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal(referenceTime));

            Assert.That(start, Is.EqualTo(expectedStart));
            Assert.That(stop, Is.EqualTo(expectedStop));
        }

        [Test]
        public void Shortcut_Unknown_Throws()
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Shortcut, TimeRangeShortcut = "next year" };

            Assert.Throws<NotSupportedException>(() => DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal("2026-06-15 10:30:00")));
        }

        [TestCase(SchedulerInterval.Days, 3, "2026-03-01 10:30:00", "2026-02-26 10:30:00")]
        [TestCase(SchedulerInterval.Weeks, 2, "2026-01-05 08:00:00", "2025-12-22 08:00:00")]
        [TestCase(SchedulerInterval.Months, 1, "2026-03-31 12:00:00", "2026-02-28 12:00:00")]
        [TestCase(SchedulerInterval.Years, 1, "2024-02-29 12:00:00", "2023-02-28 12:00:00")]
        public void Interval_EndsAtReferenceTime(SchedulerInterval interval, int offset, string referenceTime, string expectedStart)
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Interval, Interval = interval, Offset = offset };

            (string start, string stop) = DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal(referenceTime));

            Assert.That(start, Is.EqualTo(FormatFullTime(expectedStart)));
            Assert.That(stop, Is.EqualTo(FormatFullTime(referenceTime)));
        }

        [Test]
        public void Interval_Unsupported_Throws()
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Interval, Interval = SchedulerInterval.Hours, Offset = 1 };

            Assert.Throws<NotSupportedException>(() => DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal("2026-06-15 10:30:00")));
        }

        [Test]
        public void FixedDates_DoNotDependOnReferenceTime()
        {
            TimeFilter timeFilter = new()
            {
                TimeRangeType = TimeRangeType.Fixeddates,
                StartTime = ParseLocal("2024-03-10 08:00:00"),
                EndTime = ParseLocal("2024-04-10 18:00:00")
            };

            (string start, string stop) = DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal("2026-12-15 10:30:00"));

            Assert.That(start, Is.EqualTo(FormatFullTime("2024-03-10 08:00:00")));
            Assert.That(stop, Is.EqualTo(FormatFullTime("2024-04-10 18:00:00")));
        }

        [Test]
        public void FixedDates_OpenStartAndEnd_UseMinAndMaxValue()
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Fixeddates, OpenStart = true, OpenEnd = true };

            (string start, string stop) = DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal("2026-12-15 10:30:00"));

            Assert.That(start, Is.EqualTo(DateTime.MinValue.ToString(DynGraphqlQuery.fullTimeFormat)));
            Assert.That(stop, Is.EqualTo(DateTime.MaxValue.ToString(DynGraphqlQuery.fullTimeFormat)));
        }

        [Test]
        public void UnknownTimeRangeType_Throws()
        {
            TimeFilter timeFilter = new() { TimeRangeType = (TimeRangeType)99 };

            Assert.Throws<NotSupportedException>(() => DynGraphqlQuery.ResolveTimeRange(timeFilter, ParseLocal("2026-06-15 10:30:00")));
        }

        [Test]
        public void WithoutReferenceTime_ResolvesAgainstCurrentTime()
        {
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Shortcut, TimeRangeShortcut = "today" };
            DateTime before = DateTime.Now;

            (string start, _) = DynGraphqlQuery.ResolveTimeRange(timeFilter);

            DateTime after = DateTime.Now;
            Assert.That(start, Is.EqualTo(before.ToString(DynGraphqlQuery.dateFormat)).Or.EqualTo(after.ToString(DynGraphqlQuery.dateFormat)));
        }

        private static DateTime ParseLocal(string dateTime)
        {
            return DateTime.SpecifyKind(DateTime.ParseExact(dateTime, kDateTimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Local);
        }

        private static string FormatFullTime(string dateTime)
        {
            return ParseLocal(dateTime).ToString(DynGraphqlQuery.fullTimeFormat);
        }
    }
}
