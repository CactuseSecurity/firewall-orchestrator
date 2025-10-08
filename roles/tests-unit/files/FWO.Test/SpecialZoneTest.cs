using NUnit.Framework;
using FWO.Compliance;
using FWO.Basics.Interfaces;
using FWO.Data;
using NetTools;
using FWO.Basics;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    public class SpecialZoneTest
    {
        [Test]
        public void CalculateInternetZone_BasicZonesWithoutOverlaps_InternetzoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone internetZone = new();

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges = new IPAddressRange[]
            {
                IpOperations.GetIPAdressRange("0.0.0.0/3"),
                IpOperations.GetIPAdressRange("64.0.0.0/3")

            };

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges = new IPAddressRange[]
            {
                IpOperations.GetIPAdressRange("128.0.0.0/3"),
                IpOperations.GetIPAdressRange("192.0.0.0/3")

            };

            List<ComplianceNetworkZone> definedAndExcludedZones = new List<ComplianceNetworkZone>
            {
                networkZoneOne,
                networkZoneTwo
            };

            IPAddressRange[] expectedInternetZone = new IPAddressRange[]
            {
                IpOperations.GetIPAdressRange("32.0.0.0/3"),
                IpOperations.GetIPAdressRange("96.0.0.0/3"),
                IpOperations.GetIPAdressRange("160.0.0.0/3"),
                IpOperations.GetIPAdressRange("224.0.0.0/3"),
            };

            // Act

            SpecialZone.CalculateInternetZone(definedAndExcludedZones, internetZone);


            // Assert
            Assert.
                That(internetZone.IPRanges,
                Is.EqualTo(expectedInternetZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));
        }

        [Test]
        public void CalculateInternetZone_BasicZonesWithOverlaps_InternetzoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone internetZone = new();

            ComplianceNetworkZone networkZoneOne = new();

            networkZoneOne.IPRanges = new IPAddressRange[]
            {
                IpOperations.GetIPAdressRange("0.0.0.0/3"),
                IpOperations.GetIPAdressRange("64.0.0.0/3")

            };

            ComplianceNetworkZone networkZoneTwo = new();

            networkZoneTwo.IPRanges = new IPAddressRange[]
            {
                IpOperations.GetIPAdressRange("128.0.0.0/3"),
                IpOperations.GetIPAdressRange("192.0.0.0/3")

            };

            ComplianceNetworkZone overlaps = new();

            overlaps.IPRanges = new IPAddressRange[]
            {
                new IPAddressRange(IPAddress.Parse("31.255.255.255"), IPAddress.Parse("32.0.0.0")),
                new IPAddressRange(IPAddress.Parse("63.255.255.255"), IPAddress.Parse("64.0.0.0")),
                new IPAddressRange(IPAddress.Parse("191.255.255.255"), IPAddress.Parse("224.0.0.0")),
            };

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

            SpecialZone.CalculateInternetZone(definedAndExcludedZones, internetZone);


            // Assert
            Assert.
                That(internetZone.IPRanges,
                Is.EqualTo(expectedInternetZone)
                .Using<IPAddressRange>((a, b) => a.ToString() == b.ToString()));


        }

    }
}