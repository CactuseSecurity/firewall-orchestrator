using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves Flow object and group references used by workflow ticket requests.
/// </summary>
internal sealed class WorkflowTicketFlowReferenceService
{
    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public WorkflowTicketFlowReferenceService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Loads all positive Flow references used by a create-ticket request.
    /// </summary>
    public async Task<FlowReferenceCatalog> ResolveAsync(CreateTicketRequest request)
    {
        List<long> networkObjectIds = request.Rules.SelectMany(rule => rule.SourceObjects.Concat(rule.DestinationObjects))
            .Concat(request.AddressGroups.SelectMany(group => group.MemberIds)).Where(id => id > 0).Distinct().ToList();
        List<long> networkGroupIds = request.Rules.SelectMany(rule => rule.SourceGroups.Concat(rule.DestinationGroups))
            .Where(id => id > 0).Distinct().ToList();
        List<long> serviceObjectIds = request.Rules.SelectMany(rule => rule.ServiceObjects)
            .Concat(request.ServiceGroups.SelectMany(group => group.MemberIds)).Where(id => id > 0).Distinct().ToList();
        List<long> serviceGroupIds = request.Rules.SelectMany(rule => rule.ServiceGroups)
            .Where(id => id > 0).Distinct().ToList();
        List<long> timeIds = request.Rules.Where(rule => rule.TimeObjectId > 0).Select(rule => rule.TimeObjectId).Distinct().ToList();

        Task<List<FlowNwObject>> networkObjectsTask = LoadAsync<FlowNwObject>(FlowQueries.getFlowAddressObjects, "nwobj_id", networkObjectIds);
        Task<List<FlowNwGroup>> networkGroupsTask = LoadAsync<FlowNwGroup>(FlowQueries.getFlowAddressGroups, "nwgrp_id", networkGroupIds);
        Task<List<FlowSvcObject>> serviceObjectsTask = LoadAsync<FlowSvcObject>(FlowQueries.getFlowServiceObjects, "svcobj_id", serviceObjectIds);
        Task<List<FlowSvcGroup>> serviceGroupsTask = LoadAsync<FlowSvcGroup>(FlowQueries.getFlowServiceGroups, "svcgrp_id", serviceGroupIds);
        Task<List<FlowTimeObject>> timeObjectsTask = LoadAsync<FlowTimeObject>(FlowQueries.getFlowTimeObjects, "timeobj_id", timeIds);
        await Task.WhenAll(networkObjectsTask, networkGroupsTask, serviceObjectsTask, serviceGroupsTask, timeObjectsTask);

        return new FlowReferenceCatalog(await networkObjectsTask, await networkGroupsTask, await serviceObjectsTask, await serviceGroupsTask, await timeObjectsTask);
    }

    private async Task<List<T>> LoadAsync<T>(string query, string idField, List<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Dictionary<string, object> where = new()
        {
            [idField] = new Dictionary<string, object> { ["_in"] = ids }
        };
        return await apiConnection.SendQueryAsync<List<T>>(query, new { where }) ?? [];
    }
}

/// <summary>
/// Contains the typed Flow entities resolved for a workflow ticket request.
/// </summary>
internal sealed class FlowReferenceCatalog
{
    public Dictionary<long, FlowNwObject> NetworkObjects { get; }
    public Dictionary<long, FlowNwGroup> NetworkGroups { get; }
    public Dictionary<long, FlowSvcObject> ServiceObjects { get; }
    public Dictionary<long, FlowSvcGroup> ServiceGroups { get; }
    public Dictionary<long, FlowTimeObject> TimeObjects { get; }

    public FlowReferenceCatalog(IEnumerable<FlowNwObject> networkObjects, IEnumerable<FlowNwGroup> networkGroups,
        IEnumerable<FlowSvcObject> serviceObjects, IEnumerable<FlowSvcGroup> serviceGroups, IEnumerable<FlowTimeObject> timeObjects)
    {
        NetworkObjects = networkObjects.ToDictionary(item => item.Id);
        NetworkGroups = networkGroups.ToDictionary(item => item.Id);
        ServiceObjects = serviceObjects.ToDictionary(item => item.Id);
        ServiceGroups = serviceGroups.ToDictionary(item => item.Id);
        TimeObjects = timeObjects.ToDictionary(item => item.Id);
    }

    public WfReqElement BuildObjectElement(long id, ElemFieldType field)
    {
        return field == ElemFieldType.service ? BuildServiceObjectElement(id, field) : BuildNetworkObjectElement(id, field);
    }

    public WfReqElement BuildGroupElement(long id, ElemFieldType field)
    {
        return field == ElemFieldType.service ? BuildServiceGroupElement(id, field) : BuildNetworkGroupElement(id, field);
    }

    private WfReqElement BuildNetworkObjectElement(long id, ElemFieldType field)
    {
        if (!NetworkObjects.TryGetValue(id, out FlowNwObject? networkObject))
        {
            throw new ArgumentException($"Unknown Flow network object id {id}.");
        }

        return new WfReqElement
        {
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = networkObject.Name,
            IpString = networkObject.IpStart,
            IpEnd = networkObject.IpEnd,
            FlowNetworkObjectId = id
        };
    }

    private WfReqElement BuildNetworkGroupElement(long id, ElemFieldType field)
    {
        if (!NetworkGroups.TryGetValue(id, out FlowNwGroup? networkGroup))
        {
            throw new ArgumentException($"Unknown Flow network group id {id}.");
        }

        return new WfReqElement
        {
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = networkGroup.Name,
            GroupName = networkGroup.Name,
            FlowNetworkGroupId = id
        };
    }

    private WfReqElement BuildServiceObjectElement(long id, ElemFieldType field)
    {
        if (!ServiceObjects.TryGetValue(id, out FlowSvcObject? serviceObject))
        {
            throw new ArgumentException($"Unknown Flow service object id {id}.");
        }

        return new WfReqElement
        {
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = serviceObject.Name,
            Port = serviceObject.PortStart,
            PortEnd = serviceObject.PortEnd,
            ProtoId = serviceObject.ProtoId,
            FlowServiceObjectId = id
        };
    }

    private WfReqElement BuildServiceGroupElement(long id, ElemFieldType field)
    {
        if (!ServiceGroups.TryGetValue(id, out FlowSvcGroup? serviceGroup))
        {
            throw new ArgumentException($"Unknown Flow service group id {id}.");
        }

        return new WfReqElement
        {
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = serviceGroup.Name,
            GroupName = serviceGroup.Name,
            FlowServiceGroupId = id
        };
    }
}
