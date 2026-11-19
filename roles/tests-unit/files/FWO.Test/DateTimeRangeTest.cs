using System.Globalization;
using FWO.Report.Filter;
using FWO.Report.Filter.Ast;
using FWO.Report.Filter.FilterTypes;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class DateTimeRangeTest
    {
        private const string kDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        // The year and month boundaries are the cases that failed before: "this month" in December built month 13,
        // "last month" in January built month 0.
        [TestCase("this month", "2026-12-15 10:30:00", "2026-12-01 00:00:00", "2027-01-01 00:00:00")]
        [TestCase("this month", "2026-06-15 10:30:00", "2026-06-01 00:00:00", "2026-07-01 00:00:00")]
        [TestCase("last month", "2026-01-15 10:30:00", "2025-12-01 00:00:00", "2026-01-01 00:00:00")]
        [TestCase("last month", "2026-06-15 10:30:00", "2026-05-01 00:00:00", "2026-06-01 00:00:00")]
        [TestCase("this year", "2026-12-31 23:59:59", "2026-01-01 00:00:00", "2027-01-01 00:00:00")]
        [TestCase("last year", "2026-01-01 00:00:00", "2025-01-01 00:00:00", "2026-01-01 00:00:00")]
        public void RelativeRange_IsResolvedAgainstReferenceTime(string value, string referenceTime, string expectedStart, string expectedEnd)
        {
            DateTimeRange range = new(CreateEqualsFilter(value), ParseLocal(referenceTime));

            Assert.That(range.Start, Is.EqualTo(ParseLocal(expectedStart)));
            Assert.That(range.End, Is.EqualTo(ParseLocal(expectedEnd)));
        }

        [Test]
        public void Now_IsResolvedToReferenceTime()
        {
            DateTime referenceTime = ParseLocal("2026-12-31 23:59:59");

            DateTimeRange range = new(CreateEqualsFilter("now"), referenceTime);

            Assert.That(range.Start, Is.EqualTo(referenceTime));
            Assert.That(range.End, Is.EqualTo(referenceTime));
        }

        [TestCase(DateTimeKind.Local)]
        [TestCase(DateTimeKind.Utc)]
        public void RelativeRange_KeepsKindOfReferenceTime(DateTimeKind kind)
        {
            DateTime referenceTime = DateTime.SpecifyKind(ParseLocal("2026-12-15 10:30:00"), kind);

            DateTimeRange range = new(CreateEqualsFilter("this month"), referenceTime);

            Assert.That(range.Start!.Value.Kind, Is.EqualTo(kind));
            Assert.That(range.End!.Value.Kind, Is.EqualTo(kind));
        }

        [Test]
        public void SingleDate_DoesNotDependOnReferenceTime()
        {
            DateTimeRange range = new(CreateEqualsFilter("2024-03-10"), ParseLocal("2026-12-15 10:30:00"));

            Assert.That(range.Start, Is.EqualTo(new DateTime(2024, 3, 10)));
            Assert.That(range.End, Is.EqualTo(new DateTime(2024, 3, 10)));
        }

        private static AstNodeFilterDateTimeRange CreateEqualsFilter(string value)
        {
            return new AstNodeFilterDateTimeRange
            {
                Name = new Token(new Range(), "lasthit", TokenKind.LastHit),
                Operator = new Token(new Range(), "=", TokenKind.EQ),
                Value = new Token(new Range(), value, TokenKind.Value)
            };
        }

        private static DateTime ParseLocal(string dateTime)
        {
            return DateTime.SpecifyKind(DateTime.ParseExact(dateTime, kDateTimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Local);
        }
    }
}
