using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class NormalizedConfigTest
    {
        [Test]
        public void FormatDatetimeZUsesIsoOffsetWithColon()
        {
            DateTime localTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);

            string formatted = NormalizedConfig.FormatDatetimeZ(localTime);

            Assert.That(formatted, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$"));
        }

        [Test]
        public void FormatDatetimeZConvertsToUtcWhenRequested()
        {
            DateTime utcTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            string formatted = NormalizedConfig.FormatDatetimeZ(utcTime, convertToUtc: true);

            Assert.That(formatted, Is.EqualTo("2026-01-02T03:04:05+00:00"));
        }
    }
}