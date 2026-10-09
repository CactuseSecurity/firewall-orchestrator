using FWO.Data;
using FWO.Data.Enums;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LogTimeRangeTest
    {
        private const int kOneWeek = 604800;
        private const int kDayAndAHalf = 129600;

        [TestCase(LogTimeRangeUnit.Seconds, 1)]
        [TestCase(LogTimeRangeUnit.Minutes, 60)]
        [TestCase(LogTimeRangeUnit.Hours, 3600)]
        [TestCase(LogTimeRangeUnit.Days, 86400)]
        [TestCase(LogTimeRangeUnit.Weeks, 604800)]
        public void SecondsPerUnit_ReturnsTheSecondsOfOneUnit(LogTimeRangeUnit unit, int expectedSeconds)
        {
            Assert.That(LogTimeRange.SecondsPerUnit(unit), Is.EqualTo(expectedSeconds));
        }

        [Test]
        public void ToSeconds_ConvertsTheEnteredValue()
        {
            Assert.That(LogTimeRange.ToSeconds(7, LogTimeRangeUnit.Days), Is.EqualTo(kOneWeek));
        }

        [Test]
        public void ToSeconds_CapsAValueTooLargeForTheStoredSeconds()
        {
            Assert.That(LogTimeRange.ToSeconds(int.MaxValue, LogTimeRangeUnit.Days), Is.EqualTo(int.MaxValue));
        }

        [TestCase(kOneWeek, 1, LogTimeRangeUnit.Weeks)]
        [TestCase(172800, 2, LogTimeRangeUnit.Days)]
        [TestCase(kDayAndAHalf, 36, LogTimeRangeUnit.Hours)]
        [TestCase(5400, 90, LogTimeRangeUnit.Minutes)]
        [TestCase(90, 90, LogTimeRangeUnit.Seconds)]
        [TestCase(0, 0, LogTimeRangeUnit.Seconds)]
        public void ToLargestExactUnit_KeepsThePeriodExact(int seconds, int expectedValue, LogTimeRangeUnit expectedUnit)
        {
            (int value, LogTimeRangeUnit unit) = LogTimeRange.ToLargestExactUnit(seconds);

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo(expectedValue));
                Assert.That(unit, Is.EqualTo(expectedUnit));
            });
        }

        [Test]
        public void Split_ReturnsTheNonZeroPartsLargestFirst()
        {
            List<(int Value, LogTimeRangeUnit Unit)> parts = LogTimeRange.Split(kDayAndAHalf + 61);

            List<(int Value, LogTimeRangeUnit Unit)> expectedParts =
            [
                (1, LogTimeRangeUnit.Days),
                (12, LogTimeRangeUnit.Hours),
                (1, LogTimeRangeUnit.Minutes),
                (1, LogTimeRangeUnit.Seconds)
            ];
            Assert.That(parts, Is.EqualTo(expectedParts));
        }

        [Test]
        public void Split_StartsWithWeeks()
        {
            List<(int Value, LogTimeRangeUnit Unit)> expectedParts = [(1, LogTimeRangeUnit.Weeks), (1, LogTimeRangeUnit.Days)];

            Assert.That(LogTimeRange.Split(kOneWeek + 86400), Is.EqualTo(expectedParts));
        }

        [Test]
        public void Split_ReturnsNothingForANonPositivePeriod()
        {
            Assert.That(LogTimeRange.Split(0), Is.Empty);
        }
    }
}
