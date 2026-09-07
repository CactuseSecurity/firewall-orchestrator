using System.Globalization;
using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;
using NetTools;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Represents the FlowCatalogService type.
/// </summary>
public sealed class FlowCatalogService : IFlowGroupResolver
{
    private readonly ApiConnection apiConnection;
    private readonly SemaphoreSlim ipProtocolCacheLock = new(1, 1);
    private IpProtocolCache? ipProtocolCache;

    private sealed class IpProtocolCache(Dictionary<int, string> names, Dictionary<string, int> idsByName)
    {
        public Dictionary<int, string> Names { get; } = names;
        public Dictionary<string, int> IdsByName { get; } = idsByName;
    }

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowCatalogService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Performs the GetAddressObjectsAsync operation.
    /// </summary>
    public async Task<List<AddressObjectResponse>> GetAddressObjectsAsync(bool? visibleInRequest)
    {
        List<FlowNwObject> flowObjects = await LoadFlowNwObjectsAsync(visibleInRequest);
        return flowObjects.Select(ToAddressObjectResponse).ToList();
    }

    /// <summary>
    /// Performs the GetAddressGroupsAsync operation.
    /// </summary>
    public async Task<List<AddressGroupResponse>> GetAddressGroupsAsync(bool? visibleInRequest)
    {
        List<FlowNwGroup> flowGroups = await LoadFlowNwGroupsAsync(visibleInRequest);
        return flowGroups.Select(ToAddressGroupResponse).ToList();
    }

    /// <summary>
    /// Performs the GetServiceObjectsAsync operation.
    /// </summary>
    public async Task<List<ServiceObjectResponse>> GetServiceObjectsAsync(bool? visibleInRequest)
    {
        List<FlowSvcObject> flowObjects = await LoadFlowSvcObjectsAsync(visibleInRequest);
        IpProtocolCache protocolCache = await GetIpProtocolCacheAsync();
        return flowObjects.Select(flowObject => ToServiceObjectResponse(flowObject, protocolCache)).ToList();
    }

    /// <summary>
    /// Performs the GetServiceGroupsAsync operation.
    /// </summary>
    public async Task<List<ServiceGroupResponse>> GetServiceGroupsAsync(bool? visibleInRequest)
    {
        List<FlowSvcGroup> flowGroups = await LoadFlowSvcGroupsAsync(visibleInRequest);
        return flowGroups.Select(ToServiceGroupResponse).ToList();
    }

    /// <summary>
    /// Resolves only the requested, request-visible Flow groups and their active members.
    /// </summary>
    public async Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(FlowGroupResolutionParameters parameters)
    {
        parameters.NetworkGroupIds ??= [];
        parameters.NetworkGroupNames ??= [];
        parameters.ServiceGroupIds ??= [];
        parameters.ServiceGroupNames ??= [];
        Task<List<FlowNwGroup>> networkGroupsTask = parameters.NetworkGroupIds.Count == 0 && parameters.NetworkGroupNames.Count == 0
            ? Task.FromResult<List<FlowNwGroup>>([])
            : apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowAddressGroups, BuildGroupResolutionVariables(
                "nwgrp_id", parameters.NetworkGroupIds, parameters.NetworkGroupNames));
        Task<List<FlowSvcGroup>> serviceGroupsTask = parameters.ServiceGroupIds.Count == 0 && parameters.ServiceGroupNames.Count == 0
            ? Task.FromResult<List<FlowSvcGroup>>([])
            : apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowServiceGroups, BuildGroupResolutionVariables(
                "svcgrp_id", parameters.ServiceGroupIds, parameters.ServiceGroupNames));
        await Task.WhenAll(networkGroupsTask, serviceGroupsTask);

        return new FlowGroupResolutionResult
        {
            NetworkGroups = ResolveGroupMatches(await networkGroupsTask ?? [], parameters.NetworkGroupIds, parameters.NetworkGroupNames)
                .Select(ToNetworkGroupResolution)
                .ToList(),
            ServiceGroups = ResolveGroupMatches(await serviceGroupsTask ?? [], parameters.ServiceGroupIds, parameters.ServiceGroupNames)
                .Select(ToServiceGroupResolution)
                .ToList()
        };
    }

    /// <summary>
    /// Performs the GetTimeObjectsAsync operation.
    /// </summary>
    public async Task<List<TimeObjectResponse>> GetTimeObjectsAsync(bool? visibleInRequest)
    {
        List<FlowTimeObject> flowObjects = await LoadFlowTimeObjectsAsync(visibleInRequest);
        return flowObjects.Select(ToTimeObjectResponse).ToList();
    }

    /// <summary>
    /// Performs the GetAddressObjectIdAsync operation.
    /// </summary>
    public async Task<AddressObjectIdResponse> GetAddressObjectIdAsync(string ipStart, string ipEnd, bool? visibleInRequest)
    {
        List<FlowNwObject> result = await apiConnection.SendQueryAsync<List<FlowNwObject>>(
            FlowQueries.getFlowAddressObjectId,
            BuildLookupQueryVariables(visibleInRequest, ("ip_start", ipStart), ("ip_end", ipEnd))) ?? [];
        FlowNwObject? flowObject = result.FirstOrDefault();
        return flowObject == null
            ? new AddressObjectIdResponse()
            : new AddressObjectIdResponse { Id = flowObject.Id, Name = flowObject.Name ?? string.Empty };
    }

    /// <summary>
    /// Performs the GetServiceObjectIdAsync operation.
    /// </summary>
    public async Task<ServiceObjectIdResponse> GetServiceObjectIdAsync(string protocol, int? portStart, int? portEnd, bool? visibleInRequest)
    {
        int? protocolId = await ResolveProtocolIdAsync(protocol);
        if (!protocolId.HasValue)
        {
            return new ServiceObjectIdResponse();
        }

        List<FlowSvcObject> result = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(
            FlowQueries.getFlowServiceObjectId,
            BuildLookupQueryVariables(
                visibleInRequest,
                ("port_start", portStart),
                ("port_end", portEnd),
                ("ip_proto_id", protocolId.Value))) ?? [];
        FlowSvcObject? flowObject = result.FirstOrDefault();
        return flowObject == null
            ? new ServiceObjectIdResponse()
            : new ServiceObjectIdResponse { Id = flowObject.Id, Name = flowObject.Name };
    }

    /// <summary>
    /// Performs the GetTimeObjectIdAsync operation.
    /// </summary>
    public async Task<TimeObjectIdResponse> GetTimeObjectIdAsync(DateTimeOffset? startTime, DateTimeOffset? endTime, bool? visibleInRequest)
    {
        List<FlowTimeObject> result = await apiConnection.SendQueryAsync<List<FlowTimeObject>>(
            FlowQueries.getFlowTimeObjectId,
            BuildLookupQueryVariables(visibleInRequest, ("start_time", startTime), ("end_time", endTime))) ?? [];
        FlowTimeObject? flowObject = result.FirstOrDefault();
        return flowObject == null
            ? new TimeObjectIdResponse()
            : new TimeObjectIdResponse { Id = flowObject.Id, Name = flowObject.Name ?? string.Empty };
    }

    private async Task<List<FlowNwObject>> LoadFlowNwObjectsAsync(bool? visibleInRequest)
    {
        return await apiConnection.SendQueryAsync<List<FlowNwObject>>(
            FlowQueries.getFlowAddressObjects,
            BuildCatalogQueryVariables(visibleInRequest)) ?? [];
    }

    private static Dictionary<string, object> BuildGroupResolutionVariables(string idFieldName, IEnumerable<long> ids, IEnumerable<string> names)
    {
        List<Dictionary<string, object>> selectors = ids.Select(id =>
                new Dictionary<string, object> { [idFieldName] = BuildLookupExpression(id) })
            .Concat(names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name =>
                new Dictionary<string, object> { ["name"] = BuildLookupExpression(name) }))
            .ToList();
        return new Dictionary<string, object>
        {
            ["where"] = new Dictionary<string, object>
            {
                ["show_in_request_module"] = BuildLookupExpression(true),
                ["_or"] = selectors
            }
        };
    }

    private static bool IsActiveAndVisible(FlowGroup group)
    {
        return !string.IsNullOrWhiteSpace(group.Name)
            && group.ShowInRequestModule
            && !string.Equals(group.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(group.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase)
            && group.RemovedDate == null;
    }

    private static bool IsActiveAndVisible(FlowNwObject flowObject)
    {
        return flowObject.ShowInRequestModule
            && !string.Equals(flowObject.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(flowObject.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase)
            && flowObject.RemovedDate == null;
    }

    private static bool IsActiveAndVisible(FlowSvcObject flowObject)
    {
        return flowObject.ShowInRequestModule
            && !string.Equals(flowObject.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(flowObject.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase)
            && flowObject.RemovedDate == null;
    }

    private static FlowNetworkGroupResolution ToNetworkGroupResolution(FlowNwGroup group)
    {
        return new FlowNetworkGroupResolution
        {
            Id = group.Id,
            Name = group.Name,
            Members = group.NwGroupMembers
                .Where(member => IsActiveAndVisible(member.NwObject))
                .Select(member => new FlowNetworkMemberResolution
                {
                    Id = member.NwObject.Id,
                    Name = member.NwObject.Name ?? string.Empty,
                    IpStart = member.NwObject.IpStart ?? string.Empty,
                    IpEnd = member.NwObject.IpEnd ?? string.Empty
                })
                .ToList()
        };
    }

    private static FlowServiceGroupResolution ToServiceGroupResolution(FlowSvcGroup group)
    {
        return new FlowServiceGroupResolution
        {
            Id = group.Id,
            Name = group.Name,
            Members = group.SvcGroupMembers
                .Where(member => IsActiveAndVisible(member.SvcObject))
                .Select(member => new FlowServiceMemberResolution
                {
                    Id = member.SvcObject.Id,
                    Name = member.SvcObject.Name,
                    PortStart = member.SvcObject.PortStart,
                    PortEnd = member.SvcObject.PortEnd,
                    ProtoId = member.SvcObject.ProtoId
                })
                .ToList()
        };
    }

    private static IEnumerable<TGroup> ResolveGroupMatches<TGroup>(IEnumerable<TGroup> groups, IEnumerable<long> ids, IEnumerable<string> names)
        where TGroup : FlowGroup
    {
        List<TGroup> activeGroups = groups.Where(IsActiveAndVisible).ToList();
        List<TGroup> idMatches = activeGroups.Where(group => ids.Contains(group.Id)).ToList();
        foreach (TGroup group in idMatches)
        {
            yield return group;
        }

        foreach (string name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            if (idMatches.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            List<TGroup> nameMatches = activeGroups
                .Where(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (nameMatches.Count == 1)
            {
                yield return nameMatches[0];
            }
        }
    }

    private async Task<List<FlowNwGroup>> LoadFlowNwGroupsAsync(bool? visibleInRequest)
    {
        return await apiConnection.SendQueryAsync<List<FlowNwGroup>>(
            FlowQueries.getFlowAddressGroups,
            BuildCatalogQueryVariables(visibleInRequest)) ?? [];
    }

    private async Task<List<FlowSvcObject>> LoadFlowSvcObjectsAsync(bool? visibleInRequest)
    {
        return await apiConnection.SendQueryAsync<List<FlowSvcObject>>(
            FlowQueries.getFlowServiceObjects,
            BuildCatalogQueryVariables(visibleInRequest)) ?? [];
    }

    private async Task<List<FlowSvcGroup>> LoadFlowSvcGroupsAsync(bool? visibleInRequest)
    {
        return await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(
            FlowQueries.getFlowServiceGroups,
            BuildCatalogQueryVariables(visibleInRequest)) ?? [];
    }

    private async Task<List<FlowTimeObject>> LoadFlowTimeObjectsAsync(bool? visibleInRequest)
    {
        return await apiConnection.SendQueryAsync<List<FlowTimeObject>>(
            FlowQueries.getFlowTimeObjects,
            BuildCatalogQueryVariables(visibleInRequest)) ?? [];
    }

    /// <summary>
    /// Loads the IP protocol lookup cache once and publishes it atomically.
    /// </summary>
    private async Task<IpProtocolCache> GetIpProtocolCacheAsync()
    {
        if (ipProtocolCache != null)
        {
            return ipProtocolCache;
        }

        await ipProtocolCacheLock.WaitAsync();
        try
        {
            if (ipProtocolCache != null)
            {
                return ipProtocolCache;
            }

            List<IpProtocol> protocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols) ?? [];
            ipProtocolCache = new IpProtocolCache(
                protocols.ToDictionary(protocol => protocol.Id, protocol => protocol.Name),
                protocols
                    .Where(protocol => !string.IsNullOrWhiteSpace(protocol.Name))
                    .GroupBy(protocol => protocol.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase));

            return ipProtocolCache;
        }
        finally
        {
            ipProtocolCacheLock.Release();
        }
    }

    private async Task<int?> ResolveProtocolIdAsync(string protocol)
    {
        if (int.TryParse(protocol, NumberStyles.Integer, CultureInfo.InvariantCulture, out int protocolId))
        {
            return protocolId;
        }

        IpProtocolCache protocolCache = await GetIpProtocolCacheAsync();
        if (protocolCache.IdsByName.TryGetValue(protocol, out int resolvedProtocolId))
        {
            return resolvedProtocolId;
        }

        return null;
    }

    /// <summary>
    /// Builds query variables for catalog requests with an optional visibility filter.
    /// </summary>
    private static Dictionary<string, object> BuildCatalogQueryVariables(bool? visibleInRequest)
    {
        return new Dictionary<string, object> { ["where"] = BuildVisibleInRequestWhereClause(visibleInRequest) };
    }

    /// <summary>
    /// Builds query variables for lookup requests with equality or null predicates.
    /// </summary>
    private static Dictionary<string, object> BuildLookupQueryVariables(bool? visibleInRequest, params (string FieldName, object? Value)[] conditions)
    {
        Dictionary<string, object> whereClause = BuildVisibleInRequestWhereClause(visibleInRequest);
        foreach ((string fieldName, object? value) in conditions)
        {
            whereClause[fieldName] = BuildLookupExpression(value);
        }

        return new Dictionary<string, object> { ["where"] = whereClause };
    }

    /// <summary>
    /// Builds a Hasura bool_exp with the optional visible-in-request filter.
    /// </summary>
    private static Dictionary<string, object> BuildVisibleInRequestWhereClause(bool? visibleInRequest)
    {
        Dictionary<string, object> whereClause = [];
        if (visibleInRequest.HasValue)
        {
            whereClause["show_in_request_module"] = BuildLookupExpression(visibleInRequest.Value);
        }

        return whereClause;
    }

    /// <summary>
    /// Builds a Hasura lookup expression for the supplied value.
    /// </summary>
    private static Dictionary<string, object> BuildLookupExpression(object? value)
    {
        return value == null
            ? new Dictionary<string, object> { ["_is_null"] = true }
            : new Dictionary<string, object> { ["_eq"] = value };
    }

    private static AddressObjectResponse ToAddressObjectResponse(FlowNwObject flowObject)
    {
        return new AddressObjectResponse
        {
            Id = flowObject.Id,
            Name = flowObject.Name ?? string.Empty,
            Type = ResolveAddressType(flowObject),
            IpStart = flowObject.IpStart ?? string.Empty,
            IpEnd = flowObject.IpEnd ?? string.Empty,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }

    /// <summary>
    /// Resolves the address object type from its IP range.
    /// </summary>
    private static string ResolveAddressType(FlowNwObject flowObject)
    {
        if (string.IsNullOrWhiteSpace(flowObject.IpStart) || string.IsNullOrWhiteSpace(flowObject.IpEnd))
        {
            return "fqdn";
        }

        string ipStartValue = flowObject.IpStart.Split('/', 2)[0];
        string ipEndValue = flowObject.IpEnd.Split('/', 2)[0];
        if (!IPAddress.TryParse(ipStartValue, out IPAddress? ipStart)
            || !IPAddress.TryParse(ipEndValue, out IPAddress? ipEnd)
            || ipStart.AddressFamily != ipEnd.AddressFamily)
        {
            Log.WriteWarning("FlowCatalogService", "ResolveAddressType - Invalid IP range: {flowObject.IpStart} - {flowObject.IpEnd}");
            return "range";
        }

        if (ipStart.Equals(ipEnd))
        {
            return "host";
        }

        try
        {
            _ = new IPAddressRange(ipStart, ipEnd).GetPrefixLength();
            return "network";
        }
        catch (FormatException)
        {
            return "range";
        }
    }

    private static AddressGroupResponse ToAddressGroupResponse(FlowNwGroup flowGroup)
    {
        return new AddressGroupResponse
        {
            Id = flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            ShowInRequest = flowGroup.ShowInRequestModule,
            Members = flowGroup.NwGroupMembers
                .Select(member => new AddressGroupResponse.AddressGroupMemberResponse
                {
                    Id = member.NwObjectId,
                    Name = member.NwObject.Name ?? string.Empty
                })
                .ToList()
        };
    }

    private ServiceObjectResponse ToServiceObjectResponse(FlowSvcObject flowObject, IpProtocolCache protocolCache)
    {
        string protocol = string.Empty;
        if (protocolCache.Names.TryGetValue(flowObject.ProtoId, out string? protocolName))
        {
            protocol = protocolName;
        }
        else if (flowObject.ProtoId > 0)
        {
            protocol = flowObject.ProtoId.ToString(CultureInfo.InvariantCulture);
        }

        return new ServiceObjectResponse
        {
            Id = flowObject.Id,
            Name = flowObject.Name,
            PortStart = flowObject.PortStart,
            PortEnd = flowObject.PortEnd,
            Protocol = protocol,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }

    private static ServiceGroupResponse ToServiceGroupResponse(FlowSvcGroup flowGroup)
    {
        return new ServiceGroupResponse
        {
            Id = flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            ShowInRequest = flowGroup.ShowInRequestModule,
            Members = flowGroup.SvcGroupMembers
                .Select(member => new ServiceGroupResponse.ServiceGroupMemberResponse
                {
                    Id = member.SvcObjectId,
                    Name = member.SvcObject.Name
                })
                .ToList()
        };
    }

    private static TimeObjectResponse ToTimeObjectResponse(FlowTimeObject flowObject)
    {
        return new TimeObjectResponse
        {
            Id = flowObject.Id,
            Name = flowObject.Name,
            StartTime = flowObject.StartTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            EndTime = flowObject.EndTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }
}
