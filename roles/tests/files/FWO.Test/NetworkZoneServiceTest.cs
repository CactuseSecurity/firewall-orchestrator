using NUnit.Framework;
using FWO.Services;
using FWO.Data;
using NetTools;
using FWO.Basics;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    public class NetworkZoneServiceTest
    {
        [Test]
        public void CalculateInternetZone_BasicZonesWithoutOverlaps_InternetZoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone internetZone = new();

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges =
            [
                IpOperations.GetIPAdressRange("0.0.0.0/3"),
                IpOperations.GetIPAdressRange("64.0.0.0/3")

            ];

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges =
            [
                IpOperations.GetIPAdressRange("128.0.0.0/3"),
                IpOperations.GetIPAdressRange("192.0.0.0/3")

            ];

            List<ComplianceNetworkZone> definedAndExcludedZones = new List<ComplianceNetworkZone>
            {
                networkZoneOne,
                networkZoneTwo
            };

            IPAddressRange[] expectedInternetZone =
            [
                IpOperations.GetIPAdressRange("32.0.0.0/3"),
                IpOperations.GetIPAdressRange("96.0.0.0/3"),
                IpOperations.GetIPAdressRange("160.0.0.0/3"),
                IpOperations.GetIPAdressRange("224.0.0.0/3"),
            ];

            // Act

            NetworkZoneService.CalculateInternetZone(internetZone, definedAndExcludedZones);


            // Assert
            Assert.
                That(internetZone.IPRanges,
                Is.EqualTo(expectedInternetZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));
        }

        [Test]
        public void CalculateInternetZone_BasicZonesWithOverlaps_InternetZoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone internetZone = new();

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges =
            [
                IpOperations.GetIPAdressRange("0.0.0.0/3"),
                IpOperations.GetIPAdressRange("64.0.0.0/3")

            ];

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges =
            [
                IpOperations.GetIPAdressRange("128.0.0.0/3"),
                IpOperations.GetIPAdressRange("192.0.0.0/3")

            ];

            ComplianceNetworkZone overlaps = new();

            overlaps.IPRanges =
            [
                new IPAddressRange(IPAddress.Parse("31.255.255.255"), IPAddress.Parse("32.0.0.0")),
                new IPAddressRange(IPAddress.Parse("63.255.255.255"), IPAddress.Parse("64.0.0.0")),
                new IPAddressRange(IPAddress.Parse("191.255.255.255"), IPAddress.Parse("224.0.0.0")),
            ];

            List<ComplianceNetworkZone> definedAndExcludedZones = new List<ComplianceNetworkZone>
            {
                networkZoneOne,
                networkZoneTwo,
                overlaps
            };

            IPAddressRange[] expectedInternetZone = new IPAddressRange[]
            {
                new IPAddressRange(IPAddress.Parse("32.0.0.1"), IPAddress.Parse("63.255.255.254")),
                IpOperations.GetIPAdressRange("96.0.0.0/3"),
                new IPAddressRange(IPAddress.Parse("160.0.0.0"), IPAddress.Parse("191.255.255.254")),
                new IPAddressRange(IPAddress.Parse("224.0.0.1"), IPAddress.Parse("255.255.255.255")),
            };

            // Act

            NetworkZoneService.CalculateInternetZone(internetZone, definedAndExcludedZones);


            // Assert
            Assert.
                That(internetZone.IPRanges,
                Is.EqualTo(expectedInternetZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));


        }

        [Test]
        public void CalculateinternalZone_NoDefinedZones_internalZoneIpRangesEqualsConfiguredRanges()
        {
            // Arrange

            ComplianceNetworkZone internalZone = new();
            List<IPAddressRange> internalZoneRanges = new()
            {
                // Private address space

                IpOperations.GetIPAdressRange("10.0.0.0/8"),        // RFC 1918
                IpOperations.GetIPAdressRange("172.16.0.0/12"),     // RFC 1918
                IpOperations.GetIPAdressRange("192.168.0.0/16"),    // RFC 1918

                // Loopback, local

                IpOperations.GetIPAdressRange("0.0.0.0/8"),         // "This network" — IANA Special-Purpose (RFC 6890)
                IpOperations.GetIPAdressRange("127.0.0.0/8"),       // Loopback (RFC 1122)
                IpOperations.GetIPAdressRange("169.254.0.0/16"),    // Link-local (APIPA) (RFC 3927)

                
                // Multicast / Broadcast

                IpOperations.GetIPAdressRange("224.0.0.0/4"),       // Multicast (RFC 5771)
                IpOperations.GetIPAdressRange("240.0.0.0/4"),       // Reserved for future use (RFC 6890)
                IpOperations.GetIPAdressRange("255.255.255.255/32"),// Limited broadcast (RFC 919 / RFC 922) 

                // Documentation / samples

                IpOperations.GetIPAdressRange("192.0.2.0/24"),      // TEST-NET-1 (documentation) (RFC 5737)
                IpOperations.GetIPAdressRange("198.51.100.0/24"),   // TEST-NET-2 (documentation) (RFC 5737)
                IpOperations.GetIPAdressRange("203.0.113.0/24"),    // TEST-NET-3 (documentation) (RFC 5737)

                // Div (benchmarking, broadcast, multicast, special purpose, etc)

                IpOperations.GetIPAdressRange("100.64.0.0/10"),     // Shared address space (Carrier-Grade NAT) (RFC 6598)
                IpOperations.GetIPAdressRange("192.0.0.0/24"),      // IETF Protocol Assignments (RFC 6890)
                IpOperations.GetIPAdressRange("198.18.0.0/15"),     // Benchmark testing of inter-network devices — (RFC 2544 / RFC 6815)
                IpOperations.GetIPAdressRange("192.88.99.0/24"),    // 6to4 Relay Anycast (deprecated; should not be routed) (RFC 7526)


            };
            List<ComplianceNetworkZone> definedZones = new();
            IPAddressRange[] expectedinternalZone = internalZoneRanges
                                                        .Where(range => !range.ToString().Equals("255.255.255.255")) // Only disjoint ranges. 255.255.255.255/32 is in 240.0.0.0/4.
                                                        .ToArray();

            // Act

            NetworkZoneService.CalculateUndefinedInternalZone(internalZone, internalZoneRanges, definedZones);

            // Assert

            Assert.
                That(internalZone.IPRanges,
                Is.EqualTo(expectedinternalZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));


        }

        [Test]
        public void CalculateinternalZone_NoOverlappingDefinedZones_internalZoneIpRangesEqualsConfiguredRanges()
        {
            // Arrange

            ComplianceNetworkZone internalZone = new();

            List<IPAddressRange> internalZoneRanges = new()
            {
                IpOperations.GetIPAdressRange("10.0.0.0/8"),        // Private address space — RFC 1918
                IpOperations.GetIPAdressRange("172.16.0.0/12"),     // Private address space — RFC 1918
                IpOperations.GetIPAdressRange("192.168.0.0/16"),    // Private address space — RFC 1918
            };

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges =
            [
                new IPAddressRange(IPAddress.Parse("0.0.0.0"),   IPAddress.Parse("9.255.255.255")),
                new IPAddressRange(IPAddress.Parse("11.0.0.0"),  IPAddress.Parse("172.15.255.255"))
            ];

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges =
            [
                new IPAddressRange(IPAddress.Parse("172.32.0.0"),IPAddress.Parse("192.167.255.255")),
                new IPAddressRange(IPAddress.Parse("192.169.0.0"), IPAddress.Parse("255.255.255.255"))
            ];

            List<ComplianceNetworkZone> definedZones = new List<ComplianceNetworkZone>
            {
                networkZoneOne,
                networkZoneTwo
            };

            IPAddressRange[] expectedinternalZone = internalZoneRanges.ToArray();

            // Act

            NetworkZoneService.CalculateUndefinedInternalZone(internalZone, internalZoneRanges, definedZones);

            // Assert

            Assert.
                That(internalZone.IPRanges,
                Is.EqualTo(expectedinternalZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));


        }
        
        [Test]
        public void CalculateinternalZone_OverlappingDefinedZones_InternalZoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone internalZone = new();

            List<IPAddressRange> internalZoneRanges = new()
            {
                IpOperations.GetIPAdressRange("10.0.0.0/8"),        // Private address space — RFC 1918
                IpOperations.GetIPAdressRange("172.16.0.0/12"),     // Private address space — RFC 1918
                IpOperations.GetIPAdressRange("192.168.0.0/16"),    // Private address space — RFC 1918
            };

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges =
            [
                new IPAddressRange(IPAddress.Parse("0.0.0.0"),  IPAddress.Parse("172.15.255.255"))
            ];

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges =
            [
                new IPAddressRange(IPAddress.Parse("172.32.0.0"),IPAddress.Parse("192.167.255.255")),
                new IPAddressRange(IPAddress.Parse("192.169.0.0"), IPAddress.Parse("255.255.255.255"))
            ];

            List<ComplianceNetworkZone> definedZones = new List<ComplianceNetworkZone>
            {
                networkZoneOne,
                networkZoneTwo
            };

            IPAddressRange[] expectedinternalZone =
            [
                IpOperations.GetIPAdressRange("172.16.0.0/12"),    
                IpOperations.GetIPAdressRange("192.168.0.0/16")
            ];

            // Act

            NetworkZoneService.CalculateUndefinedInternalZone(internalZone, internalZoneRanges, definedZones);

            // Assert

            Assert.
                That(internalZone.IPRanges,
                Is.EqualTo(expectedinternalZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));


        }

    }
}