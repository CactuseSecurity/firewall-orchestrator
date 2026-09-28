using FWO.Data;
using FWO.Basics;
using NetTools;

namespace FWO.NetworkTopology
{
    /// <summary>Data input for the algorithm.</summary>
    public sealed record MatrixData
    {
        /// <summary>Data collected from network_zone.zone.</summary>
        public List<ComplianceNetworkZone> Zones { get; init; } = [];
        /// <summary>Data collected from network_zone.ip_range.</summary>
        public List<NetworkZoneIpRange> IpRanges { get; init; } = [];
        /// <summary>Data collected from network_zone.device_ip_range_root.</summary>
        public List<NetworkZoneDeviceIpRange> RootPaths { get; init; } = [];
        /// <summary>Data collected from network_zone.device_ip_range_internet.</summary>
        public List<NetworkZoneDeviceIpRange> InternetPaths { get; init; } = [];
    }
    /// <summary>One path query against the matrix this algorithm was built for.</summary>
    public sealed record QueryInput
    {
        /// <summary>Source input of arbitrary ip ranges.</summary>
        public List<IPAddressRange> Sources { get; init; } = [];
        /// <summary>Destination input of arbitrary ip ranges.</summary>
        public List<IPAddressRange> Destinations { get; init; } = [];
    }
    /// <summary>One firewall device found on a path.</summary>
    public sealed record PathDevice
    {
        /// <summary>Device id as stored in device.dev_id.</summary>
        public int Id { get; init; }

        /// <summary>Device name as stored in device.dev_name.</summary>
        public string Name { get; init; } = "";
    }
    /// <summary>Information about zones in PathSegment.</summary>
    public enum ZoneKind
    {
        /// <summary>A zone configured in the matrix.</summary>
        Configured,
        /// <summary>The auto calculated internet zone.</summary>
        Internet,
        /// <summary>The auto calculated catch-all zone for internal addresses no zone covers.</summary>
        UndefinedInternal
    }
    /// <summary>Source or destination of a path.</summary>
    public sealed record PathEndpoint
    {
        /// <summary>Database id of network_zone.ip_range.</summary>
        public int IpRangeId { get; init; }
        /// <summary>Database id of network_zone.zone.</summary>
        public int ZoneId { get; init; }
        /// <summary>Kind of the zone this endpoint belongs to, which decides how the path is determined.</summary>
        public ZoneKind ZoneKind { get; init; }
    }
    /// <summary>
    /// The algorithm return for one combination of a source and a destination ip range.
    /// </summary>
    public sealed record PathSegment
    {
        public required PathEndpoint Source { get; init; }
        public required PathEndpoint Destination { get; init; }
        /// <summary>
        /// Devices on the path, ordered from the source towards the destination.
        /// Includes the lowest common ancestor if the two paths share one.
        /// Empty when no path applies for the zone kind combination.
        /// </summary>
        public IReadOnlyList<PathDevice> Devices { get; init; } = [];
    }
    /// <summary>
    /// Provides the algorithm that finds paths between a source and a destination input.
    /// The algorithm uses the data of the network matrix.
    /// </summary>
    public class NetworkZoneTreeAlgorithm
    {
        /// <summary>One ip range of the matrix together with its parsed address range.</summary>
        private sealed record ParsedIpRange(NetworkZoneIpRange Row, IPAddressRange Range);
        private readonly List<ParsedIpRange> parsedIpRanges = [];
        private readonly int? internetZoneId;
        private readonly int? undefinedInternalZoneId;
        private readonly Dictionary<int, List<PathDevice>> rootPathByIpRangeId;
        private readonly Dictionary<int, List<PathDevice>> internetPathByIpRangeId;

        /// <summary>Builds the indices for the input data.</summary>
        public NetworkZoneTreeAlgorithm(MatrixData matrixData)
        {
            internetZoneId = matrixData.Zones.FirstOrDefault(zone => zone.IsAutoCalculatedInternetZone)?.Id;
            undefinedInternalZoneId = matrixData.Zones.FirstOrDefault(zone => zone.IsAutoCalculatedUndefinedInternalZone)?.Id;

            foreach (NetworkZoneIpRange zoneRange in matrixData.IpRanges)
            {
                parsedIpRanges.Add(new ParsedIpRange(zoneRange, ToRange(zoneRange)));
            }

            rootPathByIpRangeId = BuildPathIndex(matrixData.RootPaths, row => row.OrderToRoot);
            internetPathByIpRangeId = BuildPathIndex(matrixData.InternetPaths, row => row.OrderToInternet);
        }

        /// <summary>
        /// Groups path rows by their ip range and converts them into devices, ordered by path position.
        /// </summary>
        private static Dictionary<int, List<PathDevice>> BuildPathIndex(
            List<NetworkZoneDeviceIpRange> pathRows,
            Func<NetworkZoneDeviceIpRange, int?> orderSelector)
        {
            Dictionary<int, List<PathDevice>> index = [];
            foreach (NetworkZoneDeviceIpRange row in pathRows.OrderBy(orderSelector))
            {
                if (!index.TryGetValue(row.IpRangeId, out List<PathDevice>? devices))
                {
                    devices = [];
                    index[row.IpRangeId] = devices;
                }
                devices.Add(new PathDevice { Id = row.DeviceId, Name = row.Device?.Name ?? "" });
            }
            return index;
        }

        /// <summary>
        /// Returns an ordered path for each combination of source and destination ip ranges
        /// in network_zone.ip_range. The lowest common ancestor is included in the path if it exists.
        /// </summary>
        public List<PathSegment> FindDevicesInPaths(QueryInput input)
        {
            List<NetworkZoneIpRange> sourceRanges = LookupRelevantRanges(input.Sources);
            List<NetworkZoneIpRange> destinationRanges = LookupRelevantRanges(input.Destinations);
            return CalculatePaths(sourceRanges, destinationRanges);
        }

        /// <summary>
        /// Finds all ranges in network_zone.ip_range that overlap with the algorithm input.
        /// </summary>
        private List<NetworkZoneIpRange> LookupRelevantRanges(List<IPAddressRange> inputRanges)
        {
            List<NetworkZoneIpRange> relevantRanges = [];
            foreach (ParsedIpRange zoneRange in parsedIpRanges)
            {
                foreach (IPAddressRange inputRange in inputRanges)
                {
                    if (IpOperations.RangeOverlapExists(zoneRange.Range, inputRange))
                    {
                        relevantRanges.Add(zoneRange.Row);
                        break;
                    }
                }
            }
            return relevantRanges;
        }

        /// <summary>
        /// Forms the cross product of all source and destination ranges
        /// and returns an ordered path for each combination.
        /// </summary>
        private List<PathSegment> CalculatePaths(
            List<NetworkZoneIpRange> sourceRanges, List<NetworkZoneIpRange> destinationRanges)
        {
            List<PathSegment> segments = [];
            List<PathEndpoint> destinationEndpoints = [.. destinationRanges.Select(ToEndpoint)];
            List<PathEndpoint> sourceEndpoints = [.. sourceRanges.Select(ToEndpoint)];
            
            foreach (PathEndpoint source in sourceEndpoints)
            {
                foreach (PathEndpoint destination in destinationEndpoints)
                {
                    segments.Add(new PathSegment
                    {
                        Source = source,
                        Destination = destination,
                        Devices = FindDevices(source, destination)
                    });
                }
            }
            return segments;
        }
        
        /// <summary>
        /// Builds a PathEndpoint from a NetworkZoneIpRange.
        /// </summary>
        private PathEndpoint ToEndpoint(NetworkZoneIpRange range)
        {
            return new PathEndpoint
            {
                IpRangeId = range.Id,
                ZoneId = range.NetworkZoneId,
                ZoneKind = ClassifyZone(range.NetworkZoneId)
            };
        }

        /// <summary>
        /// Classifies a zone by comparing it against the auto calculated zone ids.
        /// </summary>
        private ZoneKind ClassifyZone(int zoneId)
        {
            if (zoneId == internetZoneId) return ZoneKind.Internet;
            if (zoneId == undefinedInternalZoneId) return ZoneKind.UndefinedInternal;
            return ZoneKind.Configured;
        }

        /// <summary>
        /// Decides how to return path devices based on the ZoneKind of source and destination.
        /// Path is calculated with the LCA algorithm if both sides are Configured.
        /// If exactly one side is Internet then the internet path of the other side is returned.
        /// All other ZoneKind combinations return an empty list.
        /// </summary>
        private List<PathDevice> FindDevices(PathEndpoint source, PathEndpoint destination)
        {
            return (source.ZoneKind, destination.ZoneKind) switch
            {
                (ZoneKind.UndefinedInternal, _) or (_, ZoneKind.UndefinedInternal) => [],
                (ZoneKind.Internet, ZoneKind.Internet) => [],
                (ZoneKind.Internet, _) => [.. internetPathByIpRangeId.GetValueOrDefault(destination.IpRangeId) ?? []],
                (_, ZoneKind.Internet) => [.. internetPathByIpRangeId.GetValueOrDefault(source.IpRangeId) ?? []],
                (ZoneKind.Configured, ZoneKind.Configured) => LowestCommonAncestorAlgorithm(source, destination),
                _ => throw new NotSupportedException(
                    $"Unsupported zone kind combination '{source.ZoneKind}'/'{destination.ZoneKind}'.")
            };
        }

        /// <summary>
        /// Returns the ordered path through the tree between source and destination.
        /// The lowest common ancestor is included in the path if it exists.
        /// </summary>
        private List<PathDevice> LowestCommonAncestorAlgorithm(PathEndpoint source, PathEndpoint destination)
        {
            List<PathDevice> sourcePath = rootPathByIpRangeId.GetValueOrDefault(source.IpRangeId) ?? [];
            List<PathDevice> destinationPath = rootPathByIpRangeId.GetValueOrDefault(destination.IpRangeId) ?? [];

            int common = 0;
            while (common < sourcePath.Count
                && common < destinationPath.Count
                && sourcePath[^(common + 1)].Id == destinationPath[^(common + 1)].Id)
            {
                common++;
            }

            List<PathDevice> devices = [.. sourcePath.Take(sourcePath.Count - common)];
            // Add lowest common ancestor between paths
            if (common > 0)
            {
                devices.Add(sourcePath[^common]);
            }

            devices.AddRange(destinationPath.Take(destinationPath.Count - common).Reverse());
            return devices;
        }

        /// <summary>
        /// Transforms a NetworkZoneIpRange to an IPAddressRange.
        /// </summary>
        private static IPAddressRange ToRange(NetworkZoneIpRange ipRange) =>
            new(IPAddressRange.Parse(ipRange.IpRangeStart).Begin,
                IPAddressRange.Parse(ipRange.IpRangeEnd).Begin);
    }
}