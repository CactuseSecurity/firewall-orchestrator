using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class OwnerLogEntryTest
    {
        [Test]
        public void SourceAndDestinationDisplay_RemoveSingleHostMask()
        {
            OwnerLogEntry entry = new() { Source = "192.0.2.10/32", Destination = "2001:db8::1/128" };

            Assert.Multiple(() =>
            {
                Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.10"));
                Assert.That(entry.DestinationDisplay, Is.EqualTo("2001:db8::1"));
            });
        }

        [Test]
        public void SourceDisplay_KeepsAddressWithoutMask()
        {
            OwnerLogEntry entry = new() { Source = "192.0.2.10" };

            Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.10"));
        }

        [Test]
        public void SourceDisplay_KeepsNetworkMask()
        {
            OwnerLogEntry entry = new() { Source = "192.0.2.0/24" };

            Assert.That(entry.SourceDisplay, Is.EqualTo("192.0.2.0/24"));
        }

        [Test]
        public void ServiceDisplay_UsesProtocolNameAndPort()
        {
            OwnerLogEntry entry = new()
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
            OwnerLogEntry entry = new() { ServiceProtocol = 6, ServicePort = 443 };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("6/443"));
        }

        [Test]
        public void ServiceDisplay_OmitsMissingPort()
        {
            OwnerLogEntry entry = new()
            {
                ServiceProtocol = 1,
                Protocol = new NetworkProtocol { Id = 1, Name = "icmp" }
            };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("ICMP"));
        }

        [Test]
        public void ServiceDisplay_IsEmptyWithoutProtocolAndPort()
        {
            OwnerLogEntry entry = new();

            Assert.That(entry.ServiceDisplay, Is.Empty);
        }

        [Test]
        public void ServiceDisplay_ShowsPortWithoutProtocol()
        {
            OwnerLogEntry entry = new() { ServicePort = 443 };

            Assert.That(entry.ServiceDisplay, Is.EqualTo("443"));
        }

        [Test]
        public void ServiceSortKey_GroupsEntriesByProtocolThenPort()
        {
            OwnerLogEntry unknownService = new();
            OwnerLogEntry tcpLowPort = new() { ServiceProtocol = 6, ServicePort = 80 };
            OwnerLogEntry tcpHighPort = new() { ServiceProtocol = 6, ServicePort = 8080 };
            OwnerLogEntry udpPort = new() { ServiceProtocol = 17, ServicePort = 53 };
            List<OwnerLogEntry> entries = [udpPort, tcpHighPort, unknownService, tcpLowPort];

            List<OwnerLogEntry> sorted = entries.OrderBy(entry => entry.ServiceSortKey).ToList();

            List<OwnerLogEntry> expected = [unknownService, tcpLowPort, tcpHighPort, udpPort];
            Assert.That(sorted, Is.EqualTo(expected));
        }
    }
}
