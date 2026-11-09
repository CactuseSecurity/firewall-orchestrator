using FWO.Data.Enums;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LogDataImportScheduleTest
    {
        [TestCase(LogDataImportIntervalUnit.Seconds, 15, 15)]
        [TestCase(LogDataImportIntervalUnit.Minutes, 2, 120)]
        [TestCase(LogDataImportIntervalUnit.Hours, 1, 3600)]
        public void GetInterval_ConvertsConfiguredUnitToSeconds(LogDataImportIntervalUnit unit, int value, int expectedSeconds)
        {
            TimeSpan interval = LogDataImportSchedule.GetInterval(value, unit);

            Assert.That(interval.TotalSeconds, Is.EqualTo(expectedSeconds));
        }

        [TestCase(LogDataImportIntervalUnit.Seconds, "s")]
        [TestCase(LogDataImportIntervalUnit.Minutes, "m")]
        [TestCase(LogDataImportIntervalUnit.Hours, "h")]
        public void GetIntervalLogSuffix_ReturnsConfiguredUnitSuffix(LogDataImportIntervalUnit unit, string expectedSuffix)
        {
            string suffix = LogDataImportSchedule.GetIntervalLogSuffix(unit);

            Assert.That(suffix, Is.EqualTo(expectedSuffix));
        }
    }
}
