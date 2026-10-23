using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Test;

/// <summary>
/// Shared zone matrix for the path analysis tests above the algorithm: the analyzer, the service and the
/// REST endpoint. The modelled topology is:
///
///                      root
///                        |
///                     fw-core
///                    /       \
///            fw-access-a   fw-access-b
///                 |             |
///           10.1.0.0/16   10.2.0.0/16
///
/// The two auto calculated zones are part of the matrix as they are in a real installation, so that a
/// matrix holding only those zones can be told apart from one holding configured zones.
/// </summary>
internal static class PathAnalysisTestData
{
    public const int kZoneAId = 1;
    public const int kZoneBId = 2;
    public const int kInternetZoneId = 3;
    public const int kUndefinedInternalZoneId = 4;

    public const int kRangeAId = 11;
    public const int kRangeBId = 12;

    public const int kAccessADeviceId = 101;
    public const int kAccessBDeviceId = 102;
    public const int kCoreDeviceId = 103;

    public const string kAccessAName = "fw-access-a";
    public const string kAccessBName = "fw-access-b";
    public const string kCoreName = "fw-core";

    public const string kInZoneA = "10.1.0.1";
    public const string kInZoneB = "10.2.0.1";
    public const string kNetworkInZoneA = "10.1.0.0/24";

    /// <summary>
    /// Creates an api connection that answers the four matrix queries with the full test matrix.
    /// </summary>
    public static PathAnalysisMatrixApiConnection CreateApiConnection()
    {
        PathAnalysisMatrixApiConnection apiConnection = new();
        apiConnection.Zones.Add(new ComplianceNetworkZone { Id = kZoneAId, Name = "Zone A" });
        apiConnection.Zones.Add(new ComplianceNetworkZone { Id = kZoneBId, Name = "Zone B" });
        AddAutoCalculatedZones(apiConnection);

        apiConnection.IpRanges.Add(IpRange(kRangeAId, kZoneAId, "10.1.0.0", "10.1.255.255"));
        apiConnection.IpRanges.Add(IpRange(kRangeBId, kZoneBId, "10.2.0.0", "10.2.255.255"));

        apiConnection.RootPaths.Add(RootPathRow(kRangeAId, kAccessADeviceId, kAccessAName, 1));
        apiConnection.RootPaths.Add(RootPathRow(kRangeAId, kCoreDeviceId, kCoreName, 2));
        apiConnection.RootPaths.Add(RootPathRow(kRangeBId, kAccessBDeviceId, kAccessBName, 1));
        apiConnection.RootPaths.Add(RootPathRow(kRangeBId, kCoreDeviceId, kCoreName, 2));
        return apiConnection;
    }

    /// <summary>
    /// Creates an api connection whose matrix holds no zone at all, as for a matrix id that does not exist.
    /// </summary>
    public static PathAnalysisMatrixApiConnection CreateEmptyApiConnection()
    {
        return new PathAnalysisMatrixApiConnection();
    }

    /// <summary>
    /// Creates an api connection whose matrix holds only the two auto calculated zones.
    /// </summary>
    public static PathAnalysisMatrixApiConnection CreateAutoCalculatedOnlyApiConnection()
    {
        PathAnalysisMatrixApiConnection apiConnection = new();
        AddAutoCalculatedZones(apiConnection);
        return apiConnection;
    }

    private static void AddAutoCalculatedZones(PathAnalysisMatrixApiConnection apiConnection)
    {
        apiConnection.Zones.Add(new ComplianceNetworkZone { Id = kInternetZoneId, Name = "Internet", IsAutoCalculatedInternetZone = true });
        apiConnection.Zones.Add(new ComplianceNetworkZone
        {
            Id = kUndefinedInternalZoneId,
            Name = "Undefined internal",
            IsAutoCalculatedUndefinedInternalZone = true
        });
    }

    private static NetworkZoneIpRange IpRange(int id, int zoneId, string start, string end)
    {
        return new NetworkZoneIpRange { Id = id, NetworkZoneId = zoneId, IpRangeStart = start, IpRangeEnd = end };
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
}

/// <summary>
/// Api connection that answers the matrix queries of the network zone tree analyzer from in-memory lists and
/// records the variables of every query, so that a test can tell which matrix was loaded.
/// </summary>
internal sealed class PathAnalysisMatrixApiConnection : SimulatedApiConnection
{
    public List<ComplianceNetworkZone> Zones { get; } = [];
    public List<NetworkZoneIpRange> IpRanges { get; } = [];
    public List<NetworkZoneDeviceIpRange> RootPaths { get; } = [];
    public List<NetworkZoneDeviceIpRange> InternetPaths { get; } = [];

    /// <summary>
    /// Gets the serialized variables of every query sent, in the order they were sent.
    /// </summary>
    public List<string> QueriedVariables { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether every query fails, as for an unreachable api.
    /// </summary>
    public bool FailEveryQuery { get; set; }

    public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
        string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
    {
        if (FailEveryQuery)
        {
            throw new InvalidOperationException("Simulated api failure.");
        }

        QueriedVariables.Add(JsonSerializer.Serialize(variables));
        return Task.FromResult((QueryResponseType)ResponseFor(query));
    }

    private object ResponseFor(string query)
    {
        if (query == NetworkZoneQueries.getNetworkZonesForMatrix)
        {
            return Zones;
        }
        if (query == NetworkZoneQueries.getIpRangesForMatrix)
        {
            return IpRanges;
        }
        if (query == NetworkZoneQueries.getNetworkZoneDeviceIpRangeRoot)
        {
            return RootPaths;
        }
        if (query == NetworkZoneQueries.getNetworkZoneDeviceIpRangeInternet)
        {
            return InternetPaths;
        }
        throw new NotSupportedException($"Unexpected query: {query}");
    }
}
