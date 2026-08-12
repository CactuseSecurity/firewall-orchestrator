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
        public void ServiceDisplay_UsesProtocolNameAndPort()
        {
            OwnerFirewallLogEntry entry = new()
            {
                ServiceProtocol = 6,
                ServicePort = 443,
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" }
            };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("TCP/443"));
        }

        [Test]
        public void ServiceDisplay_FallsBackToProtocolNumber()
        {
            OwnerFirewallLogEntry entry = new() { ServiceProtocol = 6, ServicePort = 443 };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("6/443"));
        }

        [Test]
        public void ServiceDisplay_OmitsMissingPort()
        {
            OwnerFirewallLogEntry entry = new()
            {
                ServiceProtocol = 1,
                Protocol = new NetworkProtocol { Id = 1, Name = "icmp" }
            };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("ICMP"));
        }

        [Test]
        public void ServiceDisplay_IsEmptyWithoutProtocolAndPort()
        {
            OwnerFirewallLogEntry entry = new();

            Assert.That(entry.ServiceDisplay, Is.Empty);
        }

        [Test]
        public void ServiceDisplay_ShowsPortWithoutProtocol()
        {
            OwnerFirewallLogEntry entry = new() { ServicePort = 443 };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("443"));
        }

        [Test]
        public void ServiceSortKey_GroupsEntriesByProtocolThenPort()
        {
            OwnerFirewallLogEntry unknownService = new();
            OwnerFirewallLogEntry tcpLowPort = new() { ServiceProtocol = 6, ServicePort = 80 };
            OwnerFirewallLogEntry tcpHighPort = new() { ServiceProtocol = 6, ServicePort = 8080 };
            OwnerFirewallLogEntry udpPort = new() { ServiceProtocol = 17, ServicePort = 53 };
            List<OwnerFirewallLogEntry> entries = [udpPort, tcpHighPort, unknownService, tcpLowPort];

            List<OwnerFirewallLogEntry> sorted = entries.OrderBy(entry => entry.ServiceSortKey).ToList();

            List<OwnerFirewallLogEntry> expected = [unknownService, tcpLowPort, tcpHighPort, udpPort];
            Assert.That(sorted, Is.EqualTo(expected));
        }
    }
}
