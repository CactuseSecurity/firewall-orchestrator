using FWO.Services.SystemUsage;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class SystemUsageDisplayTest
    {
        private const long kKibiByte = 1024;

        [TestCase(0L, "0 B")]
        [TestCase(512L, "512 B")]
        [TestCase(1023L, "1023 B")]
        [TestCase(1024L, "1 KB")]
        [TestCase(1536L, "1.5 KB")]
        [TestCase(-1L, "0 B")]
        public void FormatBytes_FormatsSmallValues(long bytes, string expected)
        {
            Assert.That(SystemUsageDisplay.FormatBytes(bytes), Is.EqualTo(expected));
        }

        [Test]
        public void FormatBytes_UsesLargerUnits()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SystemUsageDisplay.FormatBytes(400 * kKibiByte * kKibiByte), Is.EqualTo("400 MB"));
                Assert.That(SystemUsageDisplay.FormatBytes(8000 * kKibiByte * kKibiByte), Is.EqualTo("7.8 GB"));
                Assert.That(SystemUsageDisplay.FormatBytes(3 * kKibiByte * kKibiByte * kKibiByte * kKibiByte), Is.EqualTo("3 TB"));
            });
        }

        [TestCase(0.0, "0 %")]
        [TestCase(42.34, "42.3 %")]
        [TestCase(-5.0, "0 %")]
        [TestCase(150.0, "100 %")]
        public void FormatPercent_ClampsAndRounds(double percent, string expected)
        {
            Assert.That(SystemUsageDisplay.FormatPercent(percent), Is.EqualTo(expected));
        }

        [Test]
        public void FormatLoadAverage_UsesInvariantDecimalSeparator()
        {
            SystemUsageSnapshot snapshot = new() { LoadAverage1 = 0.5, LoadAverage5 = 1.5, LoadAverage15 = 2.5 };

            Assert.That(SystemUsageDisplay.FormatLoadAverage(snapshot), Is.EqualTo("0.50 / 1.50 / 2.50"));
        }

        [Test]
        public void FormatDuration_ShowsDaysOnlyWhenPresent()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SystemUsageDisplay.FormatDuration(TimeSpan.FromMinutes(75)), Is.EqualTo("01:15"));
                Assert.That(SystemUsageDisplay.FormatDuration(new TimeSpan(2, 3, 14, 0)), Is.EqualTo("2d 03:14"));
                Assert.That(SystemUsageDisplay.FormatDuration(TimeSpan.FromSeconds(-10)), Is.EqualTo("00:00"));
            });
        }
    }
}
