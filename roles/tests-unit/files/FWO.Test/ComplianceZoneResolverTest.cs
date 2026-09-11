using FWO.Compliance;
using FWO.Data;
using NetTools;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneResolverTest
{
    [Test]
    public void ResolveZones_AddsSyntheticInternetFallbackWhenAutoCalculationIsDisabled()
    {
        List<ComplianceNetworkZone> result = ComplianceZoneResolver.ResolveZones(
            [new IPAddressRange(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("203.0.113.10"))],
            [
                new ComplianceNetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                }
            ],
            autoCalculatedInternetZoneActive: false,
            internetLocalZoneName: "Internet/Local");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Id, Is.LessThanOrEqualTo(0));
            Assert.That(result[0].Name, Is.EqualTo("Internet/Local"));
            Assert.That(result[0].IPRanges, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies an IPv6 range that matches no zone is reported as unassignable instead of as internet.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void ResolveZones_Ipv6RangeWithoutMatchingZone_IsReportedAsUnassignable(bool autoCalculatedInternetZoneActive)
    {
        List<IPAddressRange> ranges = [new IPAddressRange(IPAddress.Parse("2001:db8::"), IPAddress.Parse("2001:db8::ffff"))];
        List<ComplianceNetworkZone> zones = [Ipv4Zone];

        List<ComplianceNetworkZone> result = ComplianceZoneResolver.ResolveZones(
            ranges,
            zones,
            autoCalculatedInternetZoneActive,
            "Internet/Local",
            out List<IPAddressRange> unassignableRanges);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(unassignableRanges, Has.Count.EqualTo(1));
            Assert.That(unassignableRanges[0].Begin, Is.EqualTo(IPAddress.Parse("2001:db8::")));
        });
    }

    /// <summary>
    /// Verifies the internet fallback still covers the IPv4 part of a mixed request.
    /// </summary>
    [Test]
    public void ResolveZones_UnmatchedIpv4AndIpv6Ranges_KeepsInternetFallbackForIpv4Only()
    {
        List<IPAddressRange> ranges =
        [
            new IPAddressRange(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("203.0.113.10")),
            new IPAddressRange(IPAddress.Parse("2001:db8::"), IPAddress.Parse("2001:db8::ffff"))
        ];
        List<ComplianceNetworkZone> zones = [Ipv4Zone];

        List<ComplianceNetworkZone> result = ComplianceZoneResolver.ResolveZones(
            ranges,
            zones,
            autoCalculatedInternetZoneActive: false,
            "Internet/Local",
            out List<IPAddressRange> unassignableRanges);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Internet/Local"));
            Assert.That(unassignableRanges, Has.Count.EqualTo(1));
            Assert.That(unassignableRanges[0].Begin.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
        });
    }

    /// <summary>
    /// Verifies an IPv6 range covered by a configured zone is assigned to it and reported as assessable.
    /// </summary>
    [Test]
    public void ResolveZones_Ipv6RangeMatchingIpv6Zone_ReportsNoUnassignableRange()
    {
        ComplianceNetworkZone ipv6Zone = new()
        {
            Id = 11,
            Name = "IPv6 Zone",
            IPRanges = [new IPAddressRange(IPAddress.Parse("2001:db8::"), IPAddress.Parse("2001:db8::ffff"))]
        };

        List<IPAddressRange> ranges = [new IPAddressRange(IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::2"))];
        List<ComplianceNetworkZone> zones = [ipv6Zone];

        List<ComplianceNetworkZone> result = ComplianceZoneResolver.ResolveZones(
            ranges,
            zones,
            autoCalculatedInternetZoneActive: true,
            "Internet/Local",
            out List<IPAddressRange> unassignableRanges);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(11));
            Assert.That(unassignableRanges, Is.Empty);
        });
    }

    private static ComplianceNetworkZone Ipv4Zone => new()
    {
        Id = 10,
        Name = "DMZ",
        IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
    };
}
