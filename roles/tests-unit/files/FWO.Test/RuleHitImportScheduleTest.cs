using FWO.Data.Enums;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleHitImportScheduleTest
    {
        [TestCase(RuleHitImportIntervalUnit.Minutes, 30, 30)]
        [TestCase(RuleHitImportIntervalUnit.Hours, 2, 120)]
        [TestCase(RuleHitImportIntervalUnit.Days, 1, 1440)]
        public void GetInterval_ConvertsConfiguredUnitToMinutes(RuleHitImportIntervalUnit unit, int value, int expectedMinutes)
        {
            TimeSpan interval = RuleHitImportSchedule.GetInterval(value, unit);

            Assert.That(interval.TotalMinutes, Is.EqualTo(expectedMinutes));
        }

        [Test]
        public void GetInterval_TreatsNegativeValuesAsZero()
        {
            TimeSpan interval = RuleHitImportSchedule.GetInterval(-3, RuleHitImportIntervalUnit.Days);

            Assert.That(interval, Is.EqualTo(TimeSpan.Zero));
        }

        [TestCase(RuleHitImportIntervalUnit.Minutes, "m")]
        [TestCase(RuleHitImportIntervalUnit.Hours, "h")]
        [TestCase(RuleHitImportIntervalUnit.Days, "d")]
        public void GetIntervalLogSuffix_ReturnsConfiguredUnitSuffix(RuleHitImportIntervalUnit unit, string expectedSuffix)
        {
            string suffix = RuleHitImportSchedule.GetIntervalLogSuffix(unit);

            Assert.That(suffix, Is.EqualTo(expectedSuffix));
        }

        [Test]
        public void UnknownUnitIsRejected()
        {
            RuleHitImportIntervalUnit unknownUnit = (RuleHitImportIntervalUnit)99;

            Assert.Multiple(() =>
            {
                Assert.That(() => RuleHitImportSchedule.GetInterval(1, unknownUnit), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => RuleHitImportSchedule.GetIntervalLogSuffix(unknownUnit), Throws.InstanceOf<ArgumentOutOfRangeException>());
            });
        }
    }
}
