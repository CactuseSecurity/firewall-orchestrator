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
/// Resolves compliance zones for stored and draft network objects.
/// </summary>
public sealed class ComplianceZoneService
{
    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceZoneService"/> class.
    /// </summary>
    /// <param name="apiConnection">The shared API connection.</param>
    /// <param name="globalConfig">The global configuration.</param>
    public ComplianceZoneService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
    }

    /// <summary>
    /// Returns the zones defined by the configured designated zone matrix.
    /// </summary>
    public async Task<List<ComplianceDesignatedZoneResponse>> GetDesignatedZoneMatrixZonesAsync()
    {
        List<ComplianceNetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
        return zones.Select(MapDesignatedZoneResponse).ToList();
    }

    /// <summary>
    /// Returns the zones that a draft object tree would occupy.
    /// </summary>
    public async Task<List<ComplianceDesignatedZoneResponse>> ResolveZonesForObjectsAsync(GetZonesForDraftObjectsRequest request)
    {
        List<IPAddressRange> ranges = CollectRanges(request.Objects ?? []);
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

    private static List<IPAddressRange> CollectRanges(IEnumerable<GetZonesForDraftObjectsRequest.DraftObjectRequest> objects)
    {
        List<IPAddressRange> ranges = [];

        foreach (GetZonesForDraftObjectsRequest.DraftObjectRequest draftObject in objects)
        {
            ranges.AddRange(CollectRanges(draftObject));
        }

        return ranges;
    }

    private static List<IPAddressRange> CollectRanges(GetZonesForDraftObjectsRequest.DraftObjectRequest draftObject)
    {
        if (string.Equals(draftObject.Type, ObjectType.Group, StringComparison.OrdinalIgnoreCase))
        {
            return CollectRanges(draftObject.Members ?? []);
        }

        NetworkObject networkObject = new()
        {
            Name = draftObject.Name,
            IP = draftObject.IpStart,
            IpEnd = draftObject.IpEnd,
            Type = new NetworkObjectType
            {
                Name = NormalizeObjectType(draftObject.Type)
            }
        };

        return ComplianceCheck.ParseIpRange(networkObject);
    }

    private static string NormalizeObjectType(string objectType)
    {
        return (objectType ?? string.Empty).ToLowerInvariant() switch
        {
            ObjectType.Host => ObjectType.Host,
            ObjectType.Network => ObjectType.Network,
            ObjectType.IPRange => ObjectType.IPRange,
            ObjectType.Group => ObjectType.Group,
            _ => string.Empty
        };
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
