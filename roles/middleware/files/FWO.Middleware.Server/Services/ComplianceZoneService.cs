using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NetTools;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves compliance zones for stored and provided network objects.
/// </summary>
public sealed class ComplianceZoneService(ApiConnection apiConnection, GlobalConfig globalConfig)
{
    /// <summary>
    /// Returns the zones defined by the configured designated zone matrix.
    /// </summary>
    public async Task<List<ComplianceDesignatedZoneResponse>> GetDesignatedZoneMatrixZonesAsync()
    {
        List<ComplianceNetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
        return zones.Select(MapDesignatedZoneResponse).ToList();
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

        List<ComplianceNetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
        if (zones.Count == 0)
        {
            return [];
        }

        List<ComplianceNetworkZone> resolvedZones = ComplianceZoneResolver.ResolveZones(
            ranges,
            zones,
            globalConfig.AutoCalculateInternetZone,
            globalConfig.GetText("internet_local_zone"));

        return resolvedZones
            .Where(IsPersistedMatrixZone)
            .OrderBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(zone => zone.Id)
            .Select(MapDesignatedZoneResponse)
            .ToList();
    }

    private async Task<List<ComplianceNetworkZone>> LoadDesignatedZoneMatrixZonesAsync()
    {
        if (globalConfig.ComplianceDesignatedZoneMatrixId <= 0)
        {
            return [];
        }

        List<ComplianceCriterion> designatedMatrices = await apiConnection.SendQueryAsync<List<ComplianceCriterion>>(
            ComplianceQueries.getMatrixById,
            new { criterionId = globalConfig.ComplianceDesignatedZoneMatrixId }) ?? [];

        if (designatedMatrices.Count == 0)
        {
            return [];
        }

        return await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(
            ComplianceQueries.getNetworkZonesForMatrix,
            new { criterionId = globalConfig.ComplianceDesignatedZoneMatrixId }) ?? [];
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

        NetworkObject networkObject = new()
        {
            Name = leaf.Name,
            IP = leaf.IpStart,
            IpEnd = leaf.IpEnd,
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

    private static bool IsPersistedMatrixZone(ComplianceNetworkZone zone)
    {
        return zone.Id > 0;
    }

    private static ComplianceDesignatedZoneResponse MapDesignatedZoneResponse(ComplianceNetworkZone zone)
    {
        return new ComplianceDesignatedZoneResponse
        {
            Id = zone.Id,
            Name = zone.Name,
            Description = zone.Description,
            IpRanges = [.. zone.IPRanges.Select(ipRange => new ComplianceDesignatedZoneIpRangeResponse
            {
                IpStart = ipRange.Begin.ToString(),
                IpEnd = ipRange.End.ToString()
            })]
        };
    }
}
