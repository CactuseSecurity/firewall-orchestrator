using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Compliance;
using FWO.Compliance.RulePositioning;
using FWO.Config.Api;
using FWO.Data;
using FWO.NetworkTopology;
using FWO.Data.RulePositioning;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NetTools;
using DesignatedZoneMatrixReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadValue,
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadFailureCategory>;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves network zones for stored and provided network objects.
/// </summary>
public sealed class DesignatedZoneMatrixService(IDesignatedZoneMatrixReader zoneMatrixReader, GlobalConfig globalConfig)
{
    /// <summary>
    /// Returns one page of the zones defined by the configured designated zone matrix, ordered by zone id.
    /// </summary>
    /// <param name="pageSize">Maximum number of zones returned.</param>
    /// <param name="offset">Number of zones skipped before the page; null skips none.</param>
    public async Task<ListPage<ComplianceDesignatedZoneResponse>> GetDesignatedZoneMatrixZonesAsync(int pageSize, int? offset)
    {
        List<NetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
        ListPage<NetworkZone> page = ListPage<NetworkZone>.FromOrderedItems(zones.OrderBy(zone => zone.Id), pageSize, offset);
        List<ComplianceDesignatedZoneResponse> zoneResponses = page.Items.Select(MapDesignatedZoneResponse).ToList();
        return new ListPage<ComplianceDesignatedZoneResponse>(zoneResponses, page.HasMore);
    }

    /// <summary>
    /// Returns the zones that an object tree would occupy.
    /// </summary>
    public async Task<List<ComplianceDesignatedZoneResponse>> ResolveZonesForObjectsAsync(ResolveZonesForObjectsRequest request)
    {
        List<IPAddressRange> ranges = CollectRanges(request.Objects);
        if (ranges.Count == 0)
        {
            return [];
        }

        List<NetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
        if (zones.Count == 0)
        {
            return [];
        }

        List<NetworkZone> resolvedZones = NetworkZoneResolver.ResolveZones(
            ranges,
            zones,
            globalConfig.AutoCalculateInternetZone,
            globalConfig.GetText("internet_local_zone"),
            out List<IPAddressRange> unassignableRanges);

        if (unassignableRanges.Count > 0)
        {
            string rangesDescription = string.Join(", ", unassignableRanges.Select(range => $"{range.Begin}-{range.End}"));
            throw new UnassignableIpRangesException($"The following IP ranges could not be assigned to a network zone: {rangesDescription}");
        }

        return resolvedZones
            .Where(IsPersistedMatrixZone)
            .OrderBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(zone => zone.Id)
            .Select(MapDesignatedZoneResponse)
            .ToList();
    }

    private async Task<List<NetworkZone>> LoadDesignatedZoneMatrixZonesAsync()
    {
        DesignatedZoneMatrixReadResult result = await zoneMatrixReader.ReadAsync(
            globalConfig.DesignatedZoneMatrixId,
            CancellationToken.None);
        if (result.TryGetValue(out DesignatedZoneMatrixReadValue? value))
        {
            return [.. value.NetworkZones];
        }
        if (result.TryGetFailure(out DesignatedZoneMatrixReadFailureCategory failureCategory)
            && failureCategory is DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid
                or DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable)
        {
            return [];
        }

        string diagnostic = (result.TryGetFailure(out failureCategory)
            ? failureCategory
            : DesignatedZoneMatrixReadFailureCategory.QueryFailed).GetDiagnostic();
        throw new InvalidOperationException(diagnostic);
    }

    private static List<IPAddressRange> CollectRanges(IEnumerable<ResolveZonesForObjectsRequest.ObjectRequest> objects)
    {
        List<IPAddressRange> ranges = [];

        foreach (ResolveZonesForObjectsRequest.ObjectRequest node in objects)
        {
            ranges.AddRange(CollectRanges(node));
        }

        return ranges;
    }

    private static List<IPAddressRange> CollectRanges(ResolveZonesForObjectsRequest.ObjectRequest node)
    {
        if (node is ResolveZonesForObjectsRequest.GroupObjectRequest group)
        {
            return CollectRanges(group.Members);
        }

        if (node is not ResolveZonesForObjectsRequest.LeafObjectRequest leaf)
        {
            throw new InvalidOperationException($"Unsupported object node type '{node.GetType().Name}'.");
        }

        if (!AddressInputNormalizer.TryValidateAndNormalize(leaf, $"'{leaf.Name}'", out NormalizedAddressBounds bounds, out string? error))
        {
            throw new InvalidOperationException($"'{leaf.Name}' passed validation but could not be converted: {error}");
        }

        NetworkObject networkObject = new()
        {
            Name = leaf.Name,
            IP = bounds.IpStart,
            IpEnd = bounds.IpEnd,
            Type = new NetworkObjectType
            {
                Name = NormalizeObjectType(leaf.Type)
            }
        };

        return ComplianceCheck.ParseIpRange(networkObject);
    }

    private static string NormalizeObjectType(string objectType)
    {
        return objectType.ToLowerInvariant() switch
        {
            ObjectType.Host => ObjectType.Host,
            ObjectType.Network => ObjectType.Network,
            ObjectType.IPRange => ObjectType.IPRange,
            _ => string.Empty
        };
    }

    private static bool IsPersistedMatrixZone(NetworkZone zone)
    {
        return zone.Id > 0;
    }

    private static ComplianceDesignatedZoneResponse MapDesignatedZoneResponse(NetworkZone zone)
    {
        return new ComplianceDesignatedZoneResponse
        {
            Id = zone.Id,
            Name = zone.Name,
            Description = zone.Description,
            IpRanges = [.. zone.IPRanges.Select(ipRange => new ComplianceDesignatedZoneIpRangeResponse
            {
                IpRange = [ipRange.Begin.ToString(), ipRange.End.ToString()]
            })]
        };
    }
}
