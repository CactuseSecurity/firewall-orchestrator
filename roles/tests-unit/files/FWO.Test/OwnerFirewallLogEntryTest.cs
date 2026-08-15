using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class OwnerFirewallLogEntryTest
    {
        [Test]
        public void SourceAndDestinationDisplay_RemoveSingleHostMask()
        {
            OwnerFirewallLogEntry entry = new() { Source = "192.0.2.10/32", Destination = "2001:db8::1/128" };

            Assert.Multiple(() =>
            {
                Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.10"));
                Assert.That(entry.DestinationDisplay, Is.EqualTo("2001:db8::1"));
            });
        }

        [Test]
        public void SourceDisplay_KeepsAddressWithoutMask()
        {
            OwnerFirewallLogEntry entry = new() { Source = "192.0.2.10" };

            Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.10"));
        }

        [Test]
        public void SourceDisplay_KeepsNetworkMask()
        {
            OwnerFirewallLogEntry entry = new() { Source = "192.0.2.0/24" };

            Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.0/24"));
        }

        [Test]
        public void ProtocolDisplay_UsesTheProtocolName()
        {
            OwnerFirewallLogEntry entry = new()
            {
                ServiceProtocol = 6,
                ServicePort = 443,
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" }
            };

            Assert.That(entry.ProtocolDisplay, Is.EqualTo("TCP"));
        }

        [Test]
        public void ProtocolDisplay_FallsBackToProtocolNumber()
        {
            OwnerFirewallLogEntry entry = new() { ServiceProtocol = 47 };

            Assert.That(entry.ProtocolDisplay, Is.EqualTo("47"));
        }

        [Test]
        public void ProtocolDisplay_IsEmptyWithoutProtocol()
        {
            OwnerFirewallLogEntry entry = new() { ServicePort = 443 };

            Assert.That(entry.ProtocolDisplay, Is.Empty);
        }

        [Test]
        public void ServicePort_SortsNumericallyWithinAProtocol()
        {
            OwnerFirewallLogEntry httpPort = new() { ServiceProtocol = 6, ServicePort = 80, Protocol = new NetworkProtocol { Id = 6, Name = "tcp" } };
            OwnerFirewallLogEntry httpsPort = new() { ServiceProtocol = 6, ServicePort = 443, Protocol = new NetworkProtocol { Id = 6, Name = "tcp" } };
            OwnerFirewallLogEntry highPort = new() { ServiceProtocol = 6, ServicePort = 1024, Protocol = new NetworkProtocol { Id = 6, Name = "tcp" } };
            List<OwnerFirewallLogEntry> entries = [highPort, httpsPort, httpPort];

            List<int?> sortedPorts = entries.OrderBy(entry => entry.ProtocolDisplay).ThenBy(entry => entry.ServicePort)
                .Select(entry => entry.ServicePort).ToList();

            // the table sorts on the port itself, a formatted "TCP/1024" would come before "TCP/443"
            List<int?> expected = [80, 443, 1024];
            Assert.That(sortedPorts, Is.EqualTo(expected));
        }

        [Test]
        public void LogTimeLocal_ConvertsTheStoredOffsetIntoLocalTime()
        {
            DateTimeOffset logTime = new(2026, 8, 12, 8, 15, 0, TimeSpan.FromHours(2));
            OwnerFirewallLogEntry entry = new() { LogTime = logTime };

            Assert.Multiple(() =>
            {
                Assert.That(entry.LogTimeLocal, Is.EqualTo(logTime.ToLocalTime().DateTime));
                Assert.That(entry.LogTimeLocal.Kind, Is.Not.EqualTo(DateTimeKind.Utc), "the table filters on local time");
            });
        }
    }
}
