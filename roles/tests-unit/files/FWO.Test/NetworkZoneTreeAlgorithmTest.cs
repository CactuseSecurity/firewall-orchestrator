using FWO.Data;
using FWO.NetworkTopology;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the network zone tree path analysis algorithm against a hand built matrix.
/// The modelled topology is:
///
///                      root
///                        |
///                    fw-border
///                        |
///                     fw-core
///                    /       \
///            fw-access-a   fw-access-b
///                 |             |
///           10.1.0.0/16   10.2.0.0/16
///
/// Zone C carries a root path but no internet path, zone D carries no path at all,
/// which the import format allows and documents as "expect no gateways on path".
/// </summary>
[TestFixture]
internal class NetworkZoneTreeAlgorithmTest
{
    private const int kZoneAId = 1;
    private const int kZoneBId = 2;
    private const int kInternetZoneId = 3;
    private const int kUndefinedInternalZoneId = 4;
    private const int kZoneCId = 5;
    private const int kZoneDId = 6;

    private const int kRangeAId = 11;
    private const int kRangeBId = 12;
    private const int kInternetRangeId = 13;
    private const int kUndefinedRangeId = 14;
    private const int kRangeCId = 15;
    private const int kRangeDId = 16;

    private const int kAccessADeviceId = 101;
    private const int kAccessBDeviceId = 102;
    private const int kCoreDeviceId = 103;
    private const int kBorderDeviceId = 104;
    private const int kEdgeDeviceId = 105;
    private const int kAccessCDeviceId = 106;

    private const string kAccessAName = "fw-access-a";
    private const string kAccessBName = "fw-access-b";
    private const string kCoreName = "fw-core";
    private const string kBorderName = "fw-border";
    private const string kEdgeName = "fw-edge";
    private const string kAccessCName = "fw-access-c";

    private const string kInZoneA = "10.1.0.1";
    private const string kInZoneB = "10.2.0.1";
    private const string kInZoneC = "10.3.0.1";
    private const string kInZoneD = "10.4.0.1";
    private const string kInInternetZone = "203.0.113.5";
    private const string kInUndefinedZone = "192.168.5.1";
    private const string kInNoZone = "172.31.0.1";

    /// <summary>Verifies the path between two configured zones runs up to the shared ancestor and down again.</summary>
    [Test]
    public void FindDevicesInPaths_BetweenTwoConfiguredZones_ReturnsPathViaLowestCommonAncestor()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneB);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessAName, kCoreName, kAccessBName }));
    }

    /// <summary>Verifies the shared ancestor appears once although both paths contain it.</summary>
    [Test]
    public void FindDevicesInPaths_BetweenTwoConfiguredZones_IncludesAncestorOnlyOnce()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneB);

        Assert.That(DeviceNames(segments[0]).Count(name => name == kCoreName), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies the devices above the shared ancestor are dropped. fw-border sits above fw-core on both
    /// paths and is never traversed, so it must not appear.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_BetweenTwoConfiguredZones_DropsDevicesAboveAncestor()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneB);

        Assert.That(DeviceNames(segments[0]), Does.Not.Contain(kBorderName));
    }

    /// <summary>
    /// Verifies both endpoints are reported with their ip range, zone and kind, so a caller can tell
    /// which pair a segment describes without resolving the zones again.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_ReportsEndpointsOfEachSegment()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneB);

        Assert.Multiple(() =>
        {
            Assert.That(segments[0].Source.IpRangeId, Is.EqualTo(kRangeAId));
            Assert.That(segments[0].Source.ZoneId, Is.EqualTo(kZoneAId));
            Assert.That(segments[0].Source.ZoneKind, Is.EqualTo(ZoneKind.Configured));
            Assert.That(segments[0].Destination.IpRangeId, Is.EqualTo(kRangeBId));
            Assert.That(segments[0].Destination.ZoneId, Is.EqualTo(kZoneBId));
            Assert.That(segments[0].Destination.ZoneKind, Is.EqualTo(ZoneKind.Configured));
        });
    }

    /// <summary>Verifies the device name from the path query reaches the result.</summary>
    [Test]
    public void FindDevicesInPaths_ReportsDeviceIdAndName()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneB);

        Assert.Multiple(() =>
        {
            Assert.That(segments[0].Devices[0].Id, Is.EqualTo(kAccessADeviceId));
            Assert.That(segments[0].Devices[0].Name, Is.EqualTo(kAccessAName));
        });
    }

    /// <summary>
    /// Verifies traffic within one ip range reports the gateway closest to it. The matrix asserts that
    /// gateway lies on the path, and a routing configuration or a host based firewall can send traffic
    /// through it although both addresses share a subnet.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_WithinOneIpRange_ReturnsClosestGateway()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneA);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessAName }));
    }

    /// <summary>Verifies an internet source is answered with the internet path of the destination.</summary>
    [Test]
    public void FindDevicesInPaths_FromInternetZone_ReturnsInternetPathOfDestination()
    {
        List<PathSegment> segments = Run(kInInternetZone, kInZoneA);

        Assert.Multiple(() =>
        {
            Assert.That(segments[0].Source.ZoneKind, Is.EqualTo(ZoneKind.Internet));
            Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessAName, kEdgeName }));
        });
    }

    /// <summary>Verifies an internet destination is answered with the internet path of the source.</summary>
    [Test]
    public void FindDevicesInPaths_ToInternetZone_ReturnsInternetPathOfSource()
    {
        List<PathSegment> segments = Run(kInZoneB, kInInternetZone);

        Assert.Multiple(() =>
        {
            Assert.That(segments[0].Destination.ZoneKind, Is.EqualTo(ZoneKind.Internet));
            Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessBName, kEdgeName }));
        });
    }

    /// <summary>Verifies traffic that starts and ends in the internet reports no devices of this matrix.</summary>
    [Test]
    public void FindDevicesInPaths_BetweenInternetAndInternet_ReturnsNoDevices()
    {
        List<PathSegment> segments = Run(kInInternetZone, kInInternetZone);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0].Devices, Is.Empty);
    }

    /// <summary>
    /// Verifies an ip range that has no internet path is answered with an empty list rather than an
    /// exception, which the import format allows by omitting path_to_internet.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_FromInternetZoneWithoutInternetPath_ReturnsNoDevices()
    {
        List<PathSegment> segments = Run(kInInternetZone, kInZoneC);

        Assert.That(segments[0].Devices, Is.Empty);
    }

    /// <summary>Verifies the undefined internal zone yields no path, as it carries no topology.</summary>
    [TestCase(kInUndefinedZone, kInZoneA)]
    [TestCase(kInZoneA, kInUndefinedZone)]
    [TestCase(kInUndefinedZone, kInInternetZone)]
    public void FindDevicesInPaths_WithUndefinedInternalZone_ReturnsNoDevices(string source, string destination)
    {
        List<PathSegment> segments = Run(source, destination);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0].Devices, Is.Empty);
    }

    /// <summary>Verifies the undefined internal zone is reported as such, so a caller can tell it apart from an empty path.</summary>
    [Test]
    public void FindDevicesInPaths_WithUndefinedInternalZone_ReportsZoneKind()
    {
        List<PathSegment> segments = Run(kInUndefinedZone, kInZoneA);

        Assert.That(segments[0].Source.ZoneKind, Is.EqualTo(ZoneKind.UndefinedInternal));
    }

    /// <summary>
    /// Verifies a zone without any root path contributes no devices while the other side still does,
    /// which is the documented behaviour for an omitted path_to_root.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_WithoutRootPathOnOneSide_ReturnsOnlyOtherPath()
    {
        List<PathSegment> segments = Run(kInZoneA, kInZoneD);

        Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessAName, kCoreName, kBorderName }));
    }

    /// <summary>Verifies every combination of source and destination ip range produces its own segment.</summary>
    [Test]
    public void FindDevicesInPaths_WithSeveralRangesPerSide_ReturnsCrossProduct()
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(kInZoneA));
        sources.Add(IPAddressRange.Parse(kInZoneB));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(kInZoneC));
        destinations.Add(IPAddressRange.Parse(kInInternetZone));

        QueryInput input = new() { Sources = sources, Destinations = destinations };
        List<PathSegment> segments = new NetworkZoneTreeAlgorithm(BuildMatrix()).FindDevicesInPaths(input);

        Assert.That(segments, Has.Count.EqualTo(4));
    }

    /// <summary>Verifies an input range that overlaps no zone range produces no segment at all.</summary>
    [Test]
    public void FindDevicesInPaths_WithInputOutsideEveryZone_ReturnsNoSegments()
    {
        List<PathSegment> segments = Run(kInNoZone, kInZoneA);

        Assert.That(segments, Is.Empty);
    }

    /// <summary>Verifies an empty input side produces no segments instead of throwing.</summary>
    [Test]
    public void FindDevicesInPaths_WithEmptyDestinations_ReturnsNoSegments()
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(kInZoneA));
        QueryInput input = new() { Sources = sources, Destinations = [] };

        List<PathSegment> segments = new NetworkZoneTreeAlgorithm(BuildMatrix()).FindDevicesInPaths(input);

        Assert.That(segments, Is.Empty);
    }

    /// <summary>
    /// Verifies a range that spans several zones is resolved to a segment per covered zone, so an
    /// overlapping input does not silently collapse to one zone.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_WithInputSpanningSeveralZones_ReturnsSegmentPerZone()
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse("10.0.0.0/8"));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(kInInternetZone));
        QueryInput input = new() { Sources = sources, Destinations = destinations };

        List<PathSegment> segments = new NetworkZoneTreeAlgorithm(BuildMatrix()).FindDevicesInPaths(input);

        Assert.That(segments.Select(segment => segment.Source.ZoneId),
            Is.EquivalentTo(new List<int> { kZoneAId, kZoneBId, kZoneCId, kZoneDId }));
    }

    /// <summary>
    /// Verifies the path order follows order_to_root rather than the order the rows arrive in, because
    /// the api returns them unordered.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_WithShuffledPathRows_OrdersDevicesByPathPosition()
    {
        MatrixData matrix = BuildMatrix();
        List<NetworkZoneDeviceIpRange> shuffled = [.. matrix.RootPaths.OrderByDescending(row => row.OrderToRoot)];
        MatrixData shuffledMatrix = new()
        {
            Zones = matrix.Zones,
            IpRanges = matrix.IpRanges,
            RootPaths = shuffled,
            InternetPaths = matrix.InternetPaths
        };
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(kInZoneA));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(kInZoneD));
        QueryInput input = new() { Sources = sources, Destinations = destinations };

        List<PathSegment> segments = new NetworkZoneTreeAlgorithm(shuffledMatrix).FindDevicesInPaths(input);

        Assert.That(DeviceNames(segments[0]), Is.EqualTo(new List<string> { kAccessAName, kCoreName, kBorderName }));
    }

    /// <summary>
    /// Verifies each segment owns its device list. The internet paths are cached per ip range, and
    /// handing the cached instance out would let one caller corrupt every other segment.
    /// </summary>
    [Test]
    public void FindDevicesInPaths_ReturnsIndependentDeviceListPerCall()
    {
        NetworkZoneTreeAlgorithm algorithm = new(BuildMatrix());
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(kInInternetZone));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(kInZoneA));
        QueryInput input = new() { Sources = sources, Destinations = destinations };

        List<PathSegment> first = algorithm.FindDevicesInPaths(input);
        List<PathSegment> second = algorithm.FindDevicesInPaths(input);

        Assert.Multiple(() =>
        {
            Assert.That(first[0].Devices, Is.Not.SameAs(second[0].Devices));
            Assert.That(DeviceNames(first[0]), Is.EqualTo(DeviceNames(second[0])));
        });
    }

    /// <summary>Verifies a matrix without any auto calculated zone treats every zone as configured.</summary>
    [Test]
    public void FindDevicesInPaths_WithoutAutoCalculatedZones_TreatsEveryZoneAsConfigured()
    {
        MatrixData matrix = BuildMatrix();
        List<ComplianceNetworkZone> onlyConfigured =
            [.. matrix.Zones.Where(zone => !zone.IsAutoCalculatedInternetZone && !zone.IsAutoCalculatedUndefinedInternalZone)];
        MatrixData withoutSpecialZones = new()
        {
            Zones = onlyConfigured,
            IpRanges = matrix.IpRanges,
            RootPaths = matrix.RootPaths,
            InternetPaths = matrix.InternetPaths
        };
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(kInUndefinedZone));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(kInZoneA));
        QueryInput input = new() { Sources = sources, Destinations = destinations };

        List<PathSegment> segments = new NetworkZoneTreeAlgorithm(withoutSpecialZones).FindDevicesInPaths(input);

        Assert.That(segments[0].Source.ZoneKind, Is.EqualTo(ZoneKind.Configured));
    }

    private static List<PathSegment> Run(string source, string destination)
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(source));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(destination));
        QueryInput input = new() { Sources = sources, Destinations = destinations };
        return new NetworkZoneTreeAlgorithm(BuildMatrix()).FindDevicesInPaths(input);
    }

    private static List<string> DeviceNames(PathSegment segment)
    {
        return [.. segment.Devices.Select(device => device.Name)];
    }

    private static MatrixData BuildMatrix()
    {
        List<ComplianceNetworkZone> zones = [];
        zones.Add(Zone(kZoneAId, "Zone A"));
        zones.Add(Zone(kZoneBId, "Zone B"));
        zones.Add(Zone(kZoneCId, "Zone C"));
        zones.Add(Zone(kZoneDId, "Zone D"));
        zones.Add(InternetZone(kInternetZoneId));
        zones.Add(UndefinedInternalZone(kUndefinedInternalZoneId));

        List<NetworkZoneIpRange> ipRanges = [];
        ipRanges.Add(IpRange(kRangeAId, kZoneAId, "10.1.0.0", "10.1.255.255"));
        ipRanges.Add(IpRange(kRangeBId, kZoneBId, "10.2.0.0", "10.2.255.255"));
        ipRanges.Add(IpRange(kRangeCId, kZoneCId, "10.3.0.0", "10.3.255.255"));
        ipRanges.Add(IpRange(kRangeDId, kZoneDId, "10.4.0.0", "10.4.255.255"));
        ipRanges.Add(IpRange(kInternetRangeId, kInternetZoneId, "203.0.113.0", "203.0.113.255"));
        ipRanges.Add(IpRange(kUndefinedRangeId, kUndefinedInternalZoneId, "192.168.5.0", "192.168.5.255"));

        List<NetworkZoneDeviceIpRange> rootPaths = [];
        rootPaths.Add(RootPathRow(kRangeAId, kAccessADeviceId, kAccessAName, 1));
        rootPaths.Add(RootPathRow(kRangeAId, kCoreDeviceId, kCoreName, 2));
        rootPaths.Add(RootPathRow(kRangeAId, kBorderDeviceId, kBorderName, 3));
        rootPaths.Add(RootPathRow(kRangeBId, kAccessBDeviceId, kAccessBName, 1));
        rootPaths.Add(RootPathRow(kRangeBId, kCoreDeviceId, kCoreName, 2));
        rootPaths.Add(RootPathRow(kRangeBId, kBorderDeviceId, kBorderName, 3));
        rootPaths.Add(RootPathRow(kRangeCId, kAccessCDeviceId, kAccessCName, 1));
        rootPaths.Add(RootPathRow(kRangeCId, kBorderDeviceId, kBorderName, 2));

        List<NetworkZoneDeviceIpRange> internetPaths = [];
        internetPaths.Add(InternetPathRow(kRangeAId, kAccessADeviceId, kAccessAName, 1));
        internetPaths.Add(InternetPathRow(kRangeAId, kEdgeDeviceId, kEdgeName, 2));
        internetPaths.Add(InternetPathRow(kRangeBId, kAccessBDeviceId, kAccessBName, 1));
        internetPaths.Add(InternetPathRow(kRangeBId, kEdgeDeviceId, kEdgeName, 2));

        return new MatrixData
        {
            Zones = zones,
            IpRanges = ipRanges,
            RootPaths = rootPaths,
            InternetPaths = internetPaths
        };
    }

    private static ComplianceNetworkZone Zone(int id, string name)
    {
        return new ComplianceNetworkZone { Id = id, Name = name };
    }

    private static ComplianceNetworkZone InternetZone(int id)
    {
        return new ComplianceNetworkZone { Id = id, Name = "Internet", IsAutoCalculatedInternetZone = true };
    }

    private static ComplianceNetworkZone UndefinedInternalZone(int id)
    {
        return new ComplianceNetworkZone
        {
            Id = id,
            Name = "Undefined internal",
            IsAutoCalculatedUndefinedInternalZone = true
        };
    }

    private static NetworkZoneIpRange IpRange(int id, int zoneId, string start, string end)
    {
        return new NetworkZoneIpRange
        {
            Id = id,
            NetworkZoneId = zoneId,
            IpRangeStart = start,
            IpRangeEnd = end
        };
    }

    private static NetworkZoneDeviceIpRange RootPathRow(int ipRangeId, int deviceId, string deviceName, int order)
    {
        return new NetworkZoneDeviceIpRange
        {
            IpRangeId = ipRangeId,
            DeviceId = deviceId,
            OrderToRoot = order,
            Device = new Device { Id = deviceId, Name = deviceName }
        };
    }

    private static NetworkZoneDeviceIpRange InternetPathRow(int ipRangeId, int deviceId, string deviceName, int order)
    {
        return new NetworkZoneDeviceIpRange
        {
            IpRangeId = ipRangeId,
            DeviceId = deviceId,
            OrderToInternet = order,
            Device = new Device { Id = deviceId, Name = deviceName }
        };
    }
}
