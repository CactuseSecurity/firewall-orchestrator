using FWO.Data;
using NetTools;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

[TestFixture]
internal class ComplianceNetworkZoneOverlapTest
{
    private static ComplianceNetworkZone CreateZone(string rangeStart, string rangeEnd)
    {
        return new ComplianceNetworkZone
        {
            Id = 1,
            Name = "Zone",
            IPRanges = [CreateRange(rangeStart, rangeEnd)]
        };
    }

    private static IPAddressRange CreateRange(string rangeStart, string rangeEnd)
    {
        return new IPAddressRange(IPAddress.Parse(rangeStart), IPAddress.Parse(rangeEnd));
    }

    private static (bool OverlapFound, List<IPAddressRange> Remaining) RemoveZoneOverlap(
        ComplianceNetworkZone zone,
        string queriedStart,
        string queriedEnd)
    {
        List<IPAddressRange> queried = [CreateRange(queriedStart, queriedEnd)];
        List<List<IPAddressRange>> unseen = [[CreateRange(queriedStart, queriedEnd)]];

        bool overlapFound = zone.OverlapExists(queried, unseen);

        return (overlapFound, unseen[0]);
    }

    [Test]
    public void OverlapExists_Ipv6CompleteOverlap_RemovesRange()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("2001:db8::", "2001:db8::ff"), "2001:db8::10", "2001:db8::1f");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.True);
            Assert.That(remaining, Is.Empty);
        });
    }

    [Test]
    public void OverlapExists_Ipv6OverlapOnTheLeft_MovesRangeStart()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("2001:db8::", "2001:db8::1f"), "2001:db8::10", "2001:db8::ff");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.True);
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining[0].Begin, Is.EqualTo(IPAddress.Parse("2001:db8::20")));
            Assert.That(remaining[0].End, Is.EqualTo(IPAddress.Parse("2001:db8::ff")));
        });
    }

    [Test]
    public void OverlapExists_Ipv6OverlapOnTheRight_MovesRangeEnd()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("2001:db8::80", "2001:db8::ff"), "2001:db8::", "2001:db8::8f");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.True);
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining[0].Begin, Is.EqualTo(IPAddress.Parse("2001:db8::")));
            Assert.That(remaining[0].End, Is.EqualTo(IPAddress.Parse("2001:db8::7f")));
        });
    }

    [Test]
    public void OverlapExists_Ipv6OverlapInTheMiddle_SplitsRange()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("2001:db8::10", "2001:db8::1f"), "2001:db8::", "2001:db8::ff");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.True);
            Assert.That(remaining, Has.Count.EqualTo(2));
            Assert.That(remaining.Any(range =>
                range.Begin.Equals(IPAddress.Parse("2001:db8::")) && range.End.Equals(IPAddress.Parse("2001:db8::f"))), Is.True);
            Assert.That(remaining.Any(range =>
                range.Begin.Equals(IPAddress.Parse("2001:db8::20")) && range.End.Equals(IPAddress.Parse("2001:db8::ff"))), Is.True);
        });
    }

    [Test]
    public void OverlapExists_Ipv4OverlapInTheMiddle_SplitsRange()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("10.0.0.16", "10.0.0.31"), "10.0.0.0", "10.0.0.255");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.True);
            Assert.That(remaining, Has.Count.EqualTo(2));
            Assert.That(remaining.Any(range =>
                range.Begin.Equals(IPAddress.Parse("10.0.0.0")) && range.End.Equals(IPAddress.Parse("10.0.0.15"))), Is.True);
            Assert.That(remaining.Any(range =>
                range.Begin.Equals(IPAddress.Parse("10.0.0.32")) && range.End.Equals(IPAddress.Parse("10.0.0.255"))), Is.True);
        });
    }

    [Test]
    public void OverlapExists_MixedAddressFamilies_LeavesRangesUntouched()
    {
        (bool overlapFound, List<IPAddressRange> remaining) =
            RemoveZoneOverlap(CreateZone("2001:db8::", "2001:db8::ff"), "10.0.0.0", "10.0.0.255");

        Assert.Multiple(() =>
        {
            Assert.That(overlapFound, Is.False);
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining[0].Begin, Is.EqualTo(IPAddress.Parse("10.0.0.0")));
            Assert.That(remaining[0].End, Is.EqualTo(IPAddress.Parse("10.0.0.255")));
        });
    }
}
