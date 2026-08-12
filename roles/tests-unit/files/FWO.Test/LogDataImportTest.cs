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

            List<FirewallLogEntryInput> normalized = LogDataImport.NormalizeEntries(entries, 1, ImportTime);

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
        public void MergeDuplicateEntries_MergesSameFlowOfSameOwner()
        {
            List<FirewallLogEntryInput> entries = new()
            {
                BuildEntry(1, 3, 6, 443, ImportTime),
                BuildEntry(1, 4, 6, 443, ImportTime.AddMinutes(1)),
                BuildEntry(2, 5, 6, 443, ImportTime)
            };
            entries[1].Allowed = false;
            entries[1].LoggingRuleName = "later rule";

            List<FirewallLogEntryInput> merged = LogDataImport.MergeDuplicateEntries(entries);

            FirewallLogEntryInput mergedEntry = merged.Single(entry => entry.OwnerId == 1);
            Assert.That(merged, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(mergedEntry.LogCount, Is.EqualTo(7));
                Assert.That(mergedEntry.LogTime, Is.EqualTo(ImportTime.AddMinutes(1)));
                Assert.That(mergedEntry.Allowed, Is.False);
                Assert.That(mergedEntry.LoggingRuleName, Is.EqualTo("later rule"));
                Assert.That(merged.Single(entry => entry.OwnerId == 2).LogCount, Is.EqualTo(5));
            });
        }

        [Test]
        public void MergeDuplicateEntries_MergesFlowsWithoutService()
        {
            List<FirewallLogEntryInput> entries = new()
            {
                BuildEntry(1, 1, null, null, ImportTime),
                BuildEntry(1, 2, null, null, ImportTime),
                BuildEntry(1, 4, 1, null, ImportTime)
            };

            List<FirewallLogEntryInput> merged = LogDataImport.MergeDuplicateEntries(entries);

            Assert.That(merged, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(merged.Single(entry => entry.ServiceProtocol is null).LogCount, Is.EqualTo(3));
                Assert.That(merged.Single(entry => entry.ServiceProtocol == 1).LogCount, Is.EqualTo(4));
            });
        }

        [Test]
        public void MergeDuplicateEntries_KeepsFlowsDifferingInASingleKeyField()
        {
            List<FirewallLogEntryInput> entries = new()
            {
                BuildEntry(1, 1, 6, 443, ImportTime),
                BuildEntry(1, 1, 6, 80, ImportTime),
                BuildEntry(1, 1, 17, 443, ImportTime),
                BuildEntry(2, 1, 6, 443, ImportTime),
                BuildEntry(1, 1, 6, 443, ImportTime)
            };
            entries[4].Destination = "192.0.2.9/32";

            List<FirewallLogEntryInput> merged = LogDataImport.MergeDuplicateEntries(entries);

            Assert.That(merged, Has.Count.EqualTo(5));
        }

        [Test]
        public void MergeDuplicateEntries_KeepsLogCountWithinIntegerRange()
        {
            List<FirewallLogEntryInput> entries = new()
            {
                BuildEntry(1, int.MaxValue, 6, 443, ImportTime),
                BuildEntry(1, 1, 6, 443, ImportTime)
            };

            List<FirewallLogEntryInput> merged = LogDataImport.MergeDuplicateEntries(entries);

            Assert.That(merged, Has.Count.EqualTo(1));
            Assert.That(merged.Single().LogCount, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void MergeDuplicateEntries_ReturnsEmptyListForNoEntries()
        {
            Assert.That(LogDataImport.MergeDuplicateEntries(new List<FirewallLogEntryInput>()), Is.Empty);
        }

        private static FirewallLogEntryInput BuildEntry(int ownerId, int logCount, int? protocol, int? port, DateTimeOffset logTime)
        {
            return new FirewallLogEntryInput
            {
                OwnerId = ownerId,
                LogCount = logCount,
                Source = "192.0.2.1/32",
                Destination = "192.0.2.2/32",
                ServiceProtocol = protocol,
                ServicePort = port,
                Allowed = true,
                LogTime = logTime
            };
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
