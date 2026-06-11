using System.Globalization;
using System.Text.Json;
using System.Threading;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Represents the FlowCatalogService type.
/// </summary>
public sealed class FlowCatalogService
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
        string query = BuildLookupQuery(
            "getAddressObjectId",
            "nwobjects",
            "flow_nwobject",
            "nwobj_id",
            "name",
            BuildObjectWhereClause("ip_start", ipStart, "ip_end", ipEnd, visibleInRequest));

        List<FlowNwObject> result = await apiConnection.SendQueryAsync<List<FlowNwObject>>(query) ?? [];
        FlowNwObject? flowObject = result.FirstOrDefault();
        return flowObject == null
            ? new AddressObjectIdResponse()
            : new AddressObjectIdResponse { Id = (int)flowObject.Id, Name = flowObject.Name ?? string.Empty };
    }

    /// <summary>
    /// Performs the GetServiceObjectIdAsync operation.
    /// </summary>
    public async Task<ServiceObjectIdResponse> GetServiceObjectIdAsync(string protocol, int portStart, int portEnd, bool? visibleInRequest)
    {
        int? protocolId = await ResolveProtocolIdAsync(protocol);
        if (!protocolId.HasValue)
        {
            return new ServiceObjectIdResponse();
        }

        string query = BuildLookupQuery(
            "getServiceObjectId",
            "svcobjects",
            "flow_svcobject",
            "svcobj_id",
            "name",
            BuildObjectWhereClause("port_start", portStart, "port_end", portEnd, visibleInRequest, "ip_proto_id", protocolId.Value));

        List<FlowSvcObject> result = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(query) ?? [];
        FlowSvcObject? flowObject = result.FirstOrDefault();
        return flowObject == null
            ? new ServiceObjectIdResponse()
            : new ServiceObjectIdResponse { Id = (int)flowObject.Id, Name = flowObject.Name };
    }

    private async Task<List<FlowNwObject>> LoadFlowNwObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getAddressObjects",
            "nwobjects",
            "flow_nwobject",
            "nwobj_id",
            "flowNwObjectDetails",
            FlowQueries.flowNwObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowNwObject>>(query) ?? [];
    }

    private async Task<List<FlowNwGroup>> LoadFlowNwGroupsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getAddressGroups",
            "nwgroups",
            "flow_nwgroup",
            "nwgrp_id",
            "flowNwGroupDetails",
            FlowQueries.flowNwGroupDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowNwGroup>>(query) ?? [];
    }

    private async Task<List<FlowSvcObject>> LoadFlowSvcObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getServiceObjects",
            "svcobjects",
            "flow_svcobject",
            "svcobj_id",
            "flowSvcObjectDetails",
            FlowQueries.flowSvcObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowSvcObject>>(query) ?? [];
    }

    private async Task<List<FlowSvcGroup>> LoadFlowSvcGroupsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getServiceGroups",
            "svcgroups",
            "flow_svcgroup",
            "svcgrp_id",
            "flowSvcGroupDetails",
            FlowQueries.flowSvcGroupDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(query) ?? [];
    }

    private async Task<List<FlowTimeObject>> LoadFlowTimeObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getTimeObjects",
            "timeobjects",
            "flow_timeobject",
            "timeobj_id",
            "flowTimeObjectDetails",
            FlowQueries.flowTimeObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowTimeObject>>(query) ?? [];
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

    private static string BuildCatalogQuery(
        string operationName,
        string topLevelAlias,
        string tableName,
        string idFieldName,
        string fragmentName,
        string fragmentText,
        bool? visibleInRequest)
    {
        string visibleInRequestClause = visibleInRequest.HasValue
            ? $"where: {{ show_in_request_module: {{ _eq: {GetGraphQlBoolean(visibleInRequest.Value)} }} }}, "
            : string.Empty;

        return fragmentText + $@"query {operationName} {{
  {topLevelAlias}: {tableName}({visibleInRequestClause}order_by: [{{ name: asc }}, {{ {idFieldName}: asc }}]) {{
    ...{fragmentName}
  }}
}}";
    }

    private static string BuildLookupQuery(string operationName, string topLevelAlias, string tableName, string idFieldName, string fieldName, string whereClause)
    {
        return $@"query {operationName} {{
  {topLevelAlias}: {tableName}(where: {{ {whereClause} }}, order_by: [{{ {idFieldName}: asc }}], limit: 1) {{
    {idFieldName}
    {fieldName}
  }}
}}";
    }

    private static string BuildObjectWhereClause(string firstFieldName, string firstFieldValue, string secondFieldName, string secondFieldValue, bool? visibleInRequest, string? thirdFieldName = null, int? thirdFieldValue = null)
    {
        List<string> parts =
        [
            $"{firstFieldName}: {{ _eq: {JsonSerializer.Serialize(firstFieldValue)} }}",
                $"{secondFieldName}: {{ _eq: {JsonSerializer.Serialize(secondFieldValue)} }}"
        ];

        if (thirdFieldName != null && thirdFieldValue.HasValue)
        {
            parts.Add($"{thirdFieldName}: {{ _eq: {thirdFieldValue.Value.ToString(CultureInfo.InvariantCulture)} }}");
        }

        if (visibleInRequest.HasValue)
        {
            parts.Add($"show_in_request_module: {{ _eq: {GetGraphQlBoolean(visibleInRequest.Value)} }}");
        }

        return string.Join(", ", parts);
    }

    private static string BuildObjectWhereClause(string firstFieldName, int firstFieldValue, string secondFieldName, int secondFieldValue, bool? visibleInRequest, string? thirdFieldName = null, int? thirdFieldValue = null)
    {
        List<string> parts =
        [
            $"{firstFieldName}: {{ _eq: {firstFieldValue.ToString(CultureInfo.InvariantCulture)} }}",
                $"{secondFieldName}: {{ _eq: {secondFieldValue.ToString(CultureInfo.InvariantCulture)} }}"
        ];

        if (thirdFieldName != null && thirdFieldValue.HasValue)
        {
            parts.Add($"{thirdFieldName}: {{ _eq: {thirdFieldValue.Value.ToString(CultureInfo.InvariantCulture)} }}");
        }

        if (visibleInRequest.HasValue)
        {
            parts.Add($"show_in_request_module: {{ _eq: {GetGraphQlBoolean(visibleInRequest.Value)} }}");
        }

        return string.Join(", ", parts);
    }

    private static string GetGraphQlBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static AddressObjectResponse ToAddressObjectResponse(FlowNwObject flowObject)
    {
        return new AddressObjectResponse
        {
            Id = (int)flowObject.Id,
            Name = flowObject.Name ?? string.Empty,
            IpStart = flowObject.IpStart ?? string.Empty,
            IpEnd = flowObject.IpEnd ?? string.Empty,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }

    private static AddressGroupResponse ToAddressGroupResponse(FlowNwGroup flowGroup)
    {
        return new AddressGroupResponse
        {
            Id = (int)flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            ShowInRequest = flowGroup.ShowInRequestModule,
            Members = flowGroup.NwGroupMembers
                .Select(member => new AddressGroupResponse.AddressGroupMemberResponse
                {
                    Id = (int)member.NwObjectId,
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
            Id = (int)flowObject.Id,
            Name = flowObject.Name,
            PortStart = flowObject.PortStart ?? 0,
            PortEnd = flowObject.PortEnd ?? 0,
            Protocol = protocol,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }

    private static ServiceGroupResponse ToServiceGroupResponse(FlowSvcGroup flowGroup)
    {
        return new ServiceGroupResponse
        {
            Id = (int)flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            ShowInRequest = flowGroup.ShowInRequestModule,
            Members = flowGroup.SvcGroupMembers
                .Select(member => new ServiceGroupResponse.ServiceGroupMemberResponse
                {
                    Id = (int)member.SvcObjectId,
                    Name = member.SvcObject.Name
                })
                .ToList()
        };
    }

    private static TimeObjectResponse ToTimeObjectResponse(FlowTimeObject flowObject)
    {
        return new TimeObjectResponse
        {
            Id = (int)flowObject.Id,
            Name = flowObject.Name,
            StartTime = flowObject.StartTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            EndTime = flowObject.EndTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            State = flowObject.State,
            ShowInRequest = flowObject.ShowInRequestModule
        };
    }
}
