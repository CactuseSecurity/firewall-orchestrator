using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LogDataImportTest
    {
        private static readonly DateTimeOffset ImportTime = new(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);

        [Test]
        public void NormalizeEntries_UsesHighestCountsAndNormalizesIps()
        {
            List<LogDataImportEntry> entries = new()
            {
                new()
                {
                    AppId = "APP-LOW",
                    LogCount = 1,
                    Source = "192.0.2.1",
                    Destination = "2001:db8::1"
                },
                new()
                {
                    AppId = "APP-HIGH",
                    LogCount = 2,
                    Source = "192.0.2.2",
                    Destination = "192.0.2.3",
                    Protocol = 6,
                    Port = 443,
                    Action = "deny"
                }
            };

            List<LogEntryInput> normalized = LogDataImport.NormalizeEntries(entries, 1, ImportTime);

            Assert.That(normalized, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(normalized[0].Source, Is.EqualTo("192.0.2.2/32"));
                Assert.That(normalized[0].Destination, Is.EqualTo("192.0.2.3/32"));
                Assert.That(normalized[0].Allowed, Is.False);
                Assert.That(normalized[0].LogTime, Is.EqualTo(ImportTime));
            });
        }

        [Test]
        public void NormalizeEntries_RejectsPortForNonTransportProtocol()
        {
            List<LogDataImportEntry> entries = new()
            {
                new()
                {
                    AppId = "APP-1",
                    LogCount = 1,
                    Source = "192.0.2.1",
                    Destination = "192.0.2.2",
                    Protocol = 1,
                    Port = 8
                }
            };

            Assert.Throws<InvalidDataException>(() => LogDataImport.NormalizeEntries(entries, 1000, ImportTime));
        }
    }
}
