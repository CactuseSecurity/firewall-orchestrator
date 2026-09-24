using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;
using System.Globalization;
using System.Text.Json;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides request workflow data for flow request REST endpoints.
/// </summary>
public sealed class FlowRequestService : IDisposable
{
    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;
    private readonly ApiSubscription? configSubscription;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowRequestService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
        try
        {
            configSubscription = this.apiConnection.GetSubscription<ConfigItem[]>(
                GraphqlExceptionHandler.Handle,
                OnGlobalConfigChange,
                ConfigQueries.subscribeFlowRequestConfigChanges);
        }
        catch (Exception exception)
        {
            Log.WriteError("Flow request config", "Could not start flow-request config subscription.", exception);
        }
    }

    /// <summary>
    /// Applies refreshed request-flow config values to the shared config snapshot.
    /// </summary>
    private void OnGlobalConfigChange(ConfigItem[] configItems)
    {
        globalConfig.MergeSubscriptionUpdateHandler(configItems);
    }

    /// <summary>
    /// Creates a new workflow ticket from the high-level request payload.
    /// </summary>
    /// <param name="request">The high-level request payload.</param>
    /// <param name="requesterId">Database id of the authenticated caller.</param>
    /// <param name="callerName">Login name of the authenticated caller, recorded as changer in the change history.</param>
    public async Task<CreateRequestResponse> CreateRequestAsync(CreateRequestRequest request, int requesterId, string? callerName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateRequest(request);
        if (requesterId <= 0)
        {
            throw new ArgumentException("'requesterId' must be a positive integer.");
        }

        (WorkflowPhases ticketPhase, int ticketStateId) = await ResolveInitialRequestPhaseAndStateAsync();
        Dictionary<int, FwoOwner> ownersById = await ResolveOwnersAsync();
        Dictionary<string, int> ruleActionIds = await ResolveRuleActionIdsAsync();
        Dictionary<string, int> protocolIds = await ResolveProtocolIdsAsync();
        FlowReferenceCatalog flowReferences = await ResolveFlowReferencesAsync(request);
        WfTicket ticket = BuildTicket(request, ticketStateId, requesterId, ownersById, ruleActionIds, protocolIds, flowReferences);
        // requesterId is the id of the authenticated caller, so it is also the changer - but only when that
        // caller is named. An internal caller supplies a requester without being the user who made the change,
        // and attributing the change history entry to that requester would be wrong.
        int? changerId = string.IsNullOrWhiteSpace(callerName) ? null : requesterId;
        ticket = await SaveTicketAsync(ticket, ticketPhase, callerName, changerId);
        string status = await BuildRequestStatusAsync(ticket.StateId, tolerateExternalStateErrors: true);

        return new CreateRequestResponse
        {
            Status = status,
            RequestId = ticket.Id
        };
    }

    /// <summary>
    /// Returns the workflow ticket status and latest ticket comment.
    /// </summary>
    public async Task<GetRequestStatusResponse?> GetRequestStatusAsync(long ticketId)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        return new GetRequestStatusResponse
        {
            Status = await BuildRequestStatusAsync(ticket.StateId, tolerateExternalStateErrors: false),
            StatusComment = GetLatestTicketComment(ticket)
        };
    }

    /// <summary>
    /// Validates the create-request payload before the ticket is built.
    /// </summary>
    private static void ValidateCreateRequest(CreateRequestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestorName))
        {
            throw new ArgumentException("'requestorName' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RequestorId))
        {
            throw new ArgumentException("'requestorId' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RuleContactName))
        {
            throw new ArgumentException("'ruleContactName' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RuleContactId))
        {
            throw new ArgumentException("'ruleContactId' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("'title' must not be empty.");
        }
        if (request.Rules.Count == 0)
        {
            throw new ArgumentException("At least one rule is required.");
        }
    }

    /// <summary>
    /// Resolves the initial workflow phase and state id for a newly created request ticket.
    /// </summary>
    private async Task<(WorkflowPhases Phase, int StateId)> ResolveInitialRequestPhaseAndStateAsync()
    {
        List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates) ?? [];
        StateMatrixConfigurationSnapshot stateMatrix = await StateMatrixConfigurationRepository.Load(apiConnection, WfTaskType.master);
        int configuredStateId = globalConfig.ReqApiTicketInitialStateId;
        if (configuredStateId >= 0)
        {
            if (states.Any(state => state.Id == configuredStateId))
            {
                List<WorkflowPhases> matchingPhases = StateMatrixConfigurationRepository.GetMatchingActiveWorkflowPhases(stateMatrix, configuredStateId);
                if (matchingPhases.Count == 1)
                {
                    return (matchingPhases[0], configuredStateId);
                }

                if (matchingPhases.Count > 1)
                {
                    throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} matches multiple active workflow phases: {string.Join(", ", matchingPhases)}.");
                }

                throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} does not belong to any active workflow phase.");
            }

            throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} does not exist in the current state list.");
        }

        WorkflowPhases phase = ResolveInitialWorkflowPhase(stateMatrix);
        return (phase, stateMatrix.Matrices[phase].LowestInputState);
    }

    /// <summary>
    /// Resolves the first active workflow phase starting at request.
    /// </summary>
    private static WorkflowPhases ResolveInitialWorkflowPhase(StateMatrixConfigurationSnapshot stateMatrix)
    {
        foreach (WorkflowPhases phase in Enum.GetValues<WorkflowPhases>())
        {
            if (stateMatrix.Matrices.TryGetValue(phase, out StateMatrix? matrix) && matrix.Active)
            {
                return phase;
            }
        }

        throw new InvalidOperationException("No active workflow phase is configured for request creation.");
    }

    /// <summary>
    /// Resolves the available STM rule actions by name.
    /// </summary>
    private async Task<Dictionary<string, int>> ResolveRuleActionIdsAsync()
    {
        List<RuleAction> ruleActions = await apiConnection.SendQueryAsync<List<RuleAction>>(StmQueries.getRuleActions) ?? [];
        return ruleActions
            .Where(ruleAction => !string.IsNullOrWhiteSpace(ruleAction.Name))
            .GroupBy(ruleAction => ruleAction.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the owners visible to the middleware role.
    /// </summary>
    private async Task<Dictionary<int, FwoOwner>> ResolveOwnersAsync()
    {
        List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners) ?? [];
        return owners
            .Where(owner => owner.Id > 0)
            .GroupBy(owner => owner.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    /// <summary>
    /// Resolves the available STM IP protocol ids by name.
    /// </summary>
    private async Task<Dictionary<string, int>> ResolveProtocolIdsAsync()
    {
        List<IpProtocol> protocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols) ?? [];
        return protocols
            .Where(protocol => !string.IsNullOrWhiteSpace(protocol.Name))
            .GroupBy(protocol => protocol.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<FlowReferenceCatalog> ResolveFlowReferencesAsync(CreateRequestRequest request)
    {
        List<long> networkIds = request.Rules.SelectMany(rule => rule.SourceObjects.Concat(rule.DestinationObjects))
            .Concat(request.AddressGroups.SelectMany(group => group.MemberIds))
            .Where(id => id > 0).Distinct().ToList();
        List<long> serviceIds = request.Rules.SelectMany(rule => rule.ServiceObjects)
            .Concat(request.ServiceGroups.SelectMany(group => group.MemberIds))
            .Where(id => id > 0).Distinct().ToList();
        List<long> timeIds = request.Rules.Where(rule => rule.TimeObjectId > 0).Select(rule => rule.TimeObjectId).Distinct().ToList();

        Task<List<FlowNwObject>> networkObjectsTask = LoadFlowReferencesAsync<FlowNwObject>(FlowQueries.getFlowAddressObjects, "nwobj_id", networkIds);
        Task<List<FlowNwGroup>> networkGroupsTask = LoadFlowReferencesAsync<FlowNwGroup>(FlowQueries.getFlowAddressGroups, "nwgrp_id", networkIds);
        Task<List<FlowSvcObject>> serviceObjectsTask = LoadFlowReferencesAsync<FlowSvcObject>(FlowQueries.getFlowServiceObjects, "svcobj_id", serviceIds);
        Task<List<FlowSvcGroup>> serviceGroupsTask = LoadFlowReferencesAsync<FlowSvcGroup>(FlowQueries.getFlowServiceGroups, "svcgrp_id", serviceIds);
        Task<List<FlowTimeObject>> timeObjectsTask = LoadFlowReferencesAsync<FlowTimeObject>(FlowQueries.getFlowTimeObjects, "timeobj_id", timeIds);
        await Task.WhenAll(networkObjectsTask, networkGroupsTask, serviceObjectsTask, serviceGroupsTask, timeObjectsTask);

        return new FlowReferenceCatalog(await networkObjectsTask, await networkGroupsTask, await serviceObjectsTask, await serviceGroupsTask, await timeObjectsTask);
    }

    private async Task<List<T>> LoadFlowReferencesAsync<T>(string query, string idField, List<long> ids)
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

    private sealed class FlowReferenceCatalog
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

        public WfReqElement BuildElement(long id, ElemFieldType field)
        {
            if (field == ElemFieldType.source || field == ElemFieldType.destination)
            {
                bool hasObject = NetworkObjects.TryGetValue(id, out FlowNwObject? networkObject);
                bool hasGroup = NetworkGroups.TryGetValue(id, out FlowNwGroup? networkGroup);
                if (hasObject == hasGroup)
                {
                    throw new ArgumentException(hasObject
                        ? $"Flow network id {id} is ambiguous between an object and a group."
                        : $"Unknown Flow network object or group id {id}.");
                }

                return hasObject
                    ? new WfReqElement
                    {
                        Field = field.ToString(), RequestAction = RequestAction.create.ToString(), Name = networkObject!.Name,
                        IpString = networkObject.IpStart, IpEnd = networkObject.IpEnd, FlowNetworkObjectId = id
                    }
                    : new WfReqElement
                    {
                        Field = field.ToString(), RequestAction = RequestAction.create.ToString(), Name = networkGroup!.Name,
                        GroupName = networkGroup.Name, FlowNetworkGroupId = id
                    };
            }

            if (field == ElemFieldType.service)
            {
                bool hasObject = ServiceObjects.TryGetValue(id, out FlowSvcObject? serviceObject);
                bool hasGroup = ServiceGroups.TryGetValue(id, out FlowSvcGroup? serviceGroup);
                if (hasObject == hasGroup)
                {
                    throw new ArgumentException(hasObject
                        ? $"Flow service id {id} is ambiguous between an object and a group."
                        : $"Unknown Flow service object or group id {id}.");
                }

                return hasObject
                    ? new WfReqElement
                    {
                        Field = field.ToString(), RequestAction = RequestAction.create.ToString(), Name = serviceObject!.Name,
                        Port = serviceObject.PortStart, PortEnd = serviceObject.PortEnd, ProtoId = serviceObject.ProtoId, FlowServiceObjectId = id
                    }
                    : new WfReqElement
                    {
                        Field = field.ToString(), RequestAction = RequestAction.create.ToString(), Name = serviceGroup!.Name,
                        GroupName = serviceGroup.Name, FlowServiceGroupId = id
                    };
            }

            throw new ArgumentException($"Flow reference id {id} is not valid for field '{field}'.");
        }
    }

    /// <summary>
    /// Builds the ticket object that is persisted through the existing whole-ticket insert path.
    /// </summary>
    private WfTicket BuildTicket(CreateRequestRequest request, int ticketStateId, int requesterId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds,
        Dictionary<string, int> protocolIds, FlowReferenceCatalog flowReferences)
    {
        Dictionary<long, CreateRequestEntity> entities = BuildEntityIndex(request, protocolIds);
        List<WfReqTask> tasks = [];
        int taskNumber = 1;

        tasks.AddRange(BuildGroupTasks(request, entities, ticketStateId, flowReferences, ref taskNumber));
        tasks.AddRange(BuildRuleTasks(request, entities, ticketStateId, ownersById, ruleActionIds, flowReferences, ref taskNumber));
        CreateRequestTaskSortConfig sortConfig = CreateRequestTaskSortConfig.Parse(globalConfig.ReqCreateRequestTaskSortConfig);
        tasks = CreateRequestTaskSorter.OrderForSave(tasks, request.SortTasks, sortConfig);

        return new WfTicket
        {
            Title = request.Title,
            StateId = ticketStateId,
            Requester = BuildRequester(request, requesterId),
            Reason = BuildRequestReason(request),
            Locked = true,
            Tasks = tasks
        };
    }

    /// <summary>
    /// Resolves the requestor into a user object used by the database insert.
    /// The API payload intentionally supplies these display fields so technical callers can submit
    /// requests on behalf of someone else while the authenticated caller still provides requesterId.
    /// </summary>
    private static UiUser BuildRequester(CreateRequestRequest request, int requesterId)
    {
        return new UiUser
        {
            Name = request.RequestorName,
            Dn = request.RequestorId,
            DbId = requesterId
        };
    }

    /// <summary>
    /// Keeps the supplied request contact information available in the ticket reason field.
    /// </summary>
    private static string BuildRequestReason(CreateRequestRequest request)
    {
        return $"{request.RuleContactName} ({request.RuleContactId})";
    }

    /// <summary>
    /// Builds all group/entity lookup entries and checks for duplicate ids.
    /// </summary>
    private static Dictionary<long, CreateRequestEntity> BuildEntityIndex(CreateRequestRequest request, Dictionary<string, int> protocolIds)
    {
        Dictionary<long, CreateRequestEntity> entities = [];

        foreach (CreateRequestRequest.CreateAddressObjectRequest addressObject in request.AddressObjects)
        {
            long entityId = ParseEntityId(addressObject.Id, "address object");
            AddEntity(entities, entityId, CreateRequestEntity.FromAddressObject(entityId, addressObject));
        }

        foreach (CreateRequestRequest.CreateServiceObjectRequest serviceObject in request.ServiceObjects)
        {
            long entityId = ParseEntityId(serviceObject.Id, "service object");
            AddEntity(entities, entityId, CreateRequestEntity.FromServiceObject(entityId, serviceObject, protocolIds));
        }

        foreach (CreateRequestRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            AddEntity(entities, ParseLocalEntityId(addressGroup.Id, "address group"), CreateRequestEntity.FromAddressGroup(addressGroup));
        }

        foreach (CreateRequestRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            AddEntity(entities, ParseLocalEntityId(serviceGroup.Id, "service group"), CreateRequestEntity.FromServiceGroup(serviceGroup));
        }

        foreach (CreateRequestRequest.CreateTimeObjectRequest timeObject in request.TimeObjects)
        {
            long entityId = ParseEntityId(timeObject.Id, "time object");
            AddEntity(entities, entityId, CreateRequestEntity.FromTimeObject(entityId, timeObject));
        }

        return entities;
    }

    /// <summary>
    /// Builds the access tasks for the request rules.
    /// </summary>
    private static List<WfReqTask> BuildRuleTasks(CreateRequestRequest request, Dictionary<long, CreateRequestEntity> entities,
        int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds, FlowReferenceCatalog flowReferences, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        foreach (CreateRequestRequest.CreateRequestRuleRequest rule in request.Rules)
        {
            tasks.Add(BuildRuleTask(request, rule, entities, ticketStateId, ownersById, ruleActionIds, flowReferences, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Builds ticket tasks that create object groups.
    /// </summary>
    private static List<WfReqTask> BuildGroupTasks(CreateRequestRequest request, Dictionary<long, CreateRequestEntity> entities,
        int ticketStateId, FlowReferenceCatalog flowReferences, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        foreach (CreateRequestRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            tasks.Add(BuildNetworkGroupTask(request, addressGroup, entities, ticketStateId, flowReferences, taskNumber++));
        }

        foreach (CreateRequestRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            tasks.Add(BuildServiceGroupTask(request, serviceGroup, entities, ticketStateId, flowReferences, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Creates a task for an address group.
    /// </summary>
    private static WfReqTask BuildNetworkGroupTask(CreateRequestRequest request, CreateRequestRequest.CreateAddressGroupRequest group,
        Dictionary<long, CreateRequestEntity> entities, int ticketStateId, FlowReferenceCatalog flowReferences, int taskNumber)
    {
        CreateRequestEntity groupEntity = entities[group.Id];
        return new WfReqTask
        {
            Title = groupEntity.DisplayName,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.group_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = ticketStateId,
            AdditionalInfo = BuildGroupAdditionalInfo(request, groupEntity.DisplayName, group.Id),
            Elements = [.. group.MemberIds.Select(memberId => BuildGroupMemberElement(memberId, entities, ElemFieldType.source, flowReferences))],
            Approvals = [BuildApproval(ticketStateId)],
            Locked = true
        };
    }

    /// <summary>
    /// Creates a task for a service group.
    /// </summary>
    private static WfReqTask BuildServiceGroupTask(CreateRequestRequest request, CreateRequestRequest.CreateServiceGroupRequest group,
        Dictionary<long, CreateRequestEntity> entities, int ticketStateId, FlowReferenceCatalog flowReferences, int taskNumber)
    {
        CreateRequestEntity groupEntity = entities[group.Id];
        return new WfReqTask
        {
            Title = groupEntity.DisplayName,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.group_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = ticketStateId,
            AdditionalInfo = BuildGroupAdditionalInfo(request, groupEntity.DisplayName, group.Id),
            Elements = [.. group.MemberIds.Select(memberId => BuildGroupMemberElement(memberId, entities, ElemFieldType.service, flowReferences))],
            Approvals = [BuildApproval(ticketStateId)],
            Locked = true
        };
    }

    /// <summary>
    /// Creates an access task for one request rule.
    /// </summary>
    private static WfReqTask BuildRuleTask(CreateRequestRequest request, CreateRequestRequest.CreateRequestRuleRequest rule,
        Dictionary<long, CreateRequestEntity> entities, int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds,
        FlowReferenceCatalog flowReferences, int taskNumber)
    {
        List<WfReqElement> elements =
        [
            .. BuildReferencedElements(rule.SourceObjects, entities, ElemFieldType.source, flowReferences),
            .. BuildReferencedElements(rule.DestinationObjects, entities, ElemFieldType.destination, flowReferences),
            .. BuildReferencedElements(rule.ServiceObjects, entities, ElemFieldType.service, flowReferences)
        ];

        int ruleActionId = ResolveRuleActionId(rule.Action, ruleActionIds);
        FwoOwner? taskOwner = ResolveRuleOwner(rule.OwnerId, ownersById);

        CreateRequestEntity? timeEntity = ResolveTimeObject(rule.TimeObjectId, entities, flowReferences);

        return new WfReqTask
        {
            Title = string.IsNullOrWhiteSpace(rule.Name) ? request.Title : rule.Name,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = ticketStateId,
            RuleAction = ruleActionId,
            Tracking = 1,
            Reason = rule.ViolationJustification,
            TargetBeginDate = timeEntity?.TimeStart,
            TargetEndDate = timeEntity?.TimeEnd,
            AdditionalInfo = BuildAdditionalInfo(request.RuleContactName, request.RuleContactId, request.RequestorName, request.RequestorId, timeEntity),
            Elements = elements,
            Approvals = [BuildApproval(ticketStateId)],
            Owners = taskOwner == null ? [] : [new() { Owner = taskOwner }],
            Locked = true
        };
    }

    /// <summary>
    /// Resolves the owner id configured on a rule.
    /// </summary>
    private static FwoOwner? ResolveRuleOwner(int ownerId, Dictionary<int, FwoOwner> ownersById)
    {
        if (ownerId <= 0)
        {
            return null;
        }

        if (ownersById.TryGetValue(ownerId, out FwoOwner? owner))
        {
            return owner;
        }

        throw new ArgumentException($"Unknown owner id {ownerId}.");
    }

    /// <summary>
    /// Resolves the requested rule action id from the STM action dictionary.
    /// </summary>
    private static int ResolveRuleActionId(string action, Dictionary<string, int> ruleActionIds)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("'action' must not be empty.");
        }

        action = action.Trim();
        if (ruleActionIds.TryGetValue(action, out int ruleActionId))
        {
            return ruleActionId;
        }

        throw new ArgumentException($"Unknown rule action '{action}'.");
    }

    /// <summary>
    /// Resolves the referenced time object, if any.
    /// </summary>
    private static CreateRequestEntity? ResolveTimeObject(long timeObjectId, Dictionary<long, CreateRequestEntity> entities, FlowReferenceCatalog flowReferences)
    {
        if (timeObjectId == 0)
        {
            return null;
        }

        if (timeObjectId > 0)
        {
            if (flowReferences.TimeObjects.TryGetValue(timeObjectId, out FlowTimeObject? flowTimeObject))
            {
                return CreateRequestEntity.FromFlowTimeObject(flowTimeObject);
            }

            throw new ArgumentException($"Unknown Flow time object id {timeObjectId}.");
        }

        if (!entities.TryGetValue(timeObjectId, out CreateRequestEntity? entity) || entity.Kind != CreateRequestEntityKind.TimeObject)
        {
            throw new ArgumentException($"Time object id {timeObjectId} must reference a time object.");
        }

        return entity;
    }

    /// <summary>
    /// Builds elements for rule references.
    /// </summary>
    private static IEnumerable<WfReqElement> BuildReferencedElements(IEnumerable<long> references, Dictionary<long, CreateRequestEntity> entities, ElemFieldType field,
        FlowReferenceCatalog flowReferences)
    {
        foreach (long reference in references)
        {
            yield return BuildReferencedElement(reference, entities, field, flowReferences);
        }
    }

    /// <summary>
    /// Converts a single reference into a workflow element.
    /// </summary>
    private static WfReqElement BuildReferencedElement(long reference, Dictionary<long, CreateRequestEntity> entities, ElemFieldType field,
        FlowReferenceCatalog flowReferences)
    {
        if (reference > 0)
        {
            return flowReferences.BuildElement(reference, field);
        }

        CreateRequestEntity entity = GetEntity(entities, reference);
        if (field == ElemFieldType.service)
        {
            return entity.Kind switch
            {
                CreateRequestEntityKind.ServiceObject => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    Port = entity.PortStart,
                    PortEnd = entity.PortEnd,
                    ProtoId = entity.ProtocolId
                },
                CreateRequestEntityKind.ServiceGroup => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    GroupName = entity.DisplayName,
                },
                _ => throw new ArgumentException($"Reference id {reference} is not valid for field '{field}'. The field requires a service object or service group.")
            };
        }

        if (field == ElemFieldType.source || field == ElemFieldType.destination)
        {
            return entity.Kind switch
            {
                CreateRequestEntityKind.AddressObject => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    IpString = entity.IpStart,
                    IpEnd = entity.IpEnd
                },
                CreateRequestEntityKind.AddressGroup => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    GroupName = entity.DisplayName,
                },
                _ => throw new ArgumentException($"Reference id {reference} is not valid for field '{field}'. The field requires an address object or address group.")
            };
        }

        throw new ArgumentException($"Reference id {reference} is not valid for field '{field}'.");
    }

    /// <summary>
    /// Creates one element for a group member reference.
    /// </summary>
    private static WfReqElement BuildGroupMemberElement(long memberId, Dictionary<long, CreateRequestEntity> entities, ElemFieldType field,
        FlowReferenceCatalog flowReferences)
    {
        if (memberId > 0)
        {
            return flowReferences.BuildElement(memberId, field);
        }

        CreateRequestEntity entity = GetEntity(entities, memberId);
        return entity.Kind switch
        {
            CreateRequestEntityKind.AddressObject => new WfReqElement
            {
                Field = field.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Name = entity.DisplayName,
                IpString = entity.IpStart,
                IpEnd = entity.IpEnd
            },
            CreateRequestEntityKind.ServiceObject => new WfReqElement
            {
                Field = field.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Name = entity.DisplayName,
                Port = entity.PortStart,
                PortEnd = entity.PortEnd,
                ProtoId = entity.ProtocolId
            },
            _ => throw new ArgumentException($"Member id {memberId} cannot be used in a group.")
        };
    }

    /// <summary>
    /// Stores group metadata together with the request context.
    /// </summary>
    private static string BuildGroupAdditionalInfo(CreateRequestRequest request, string groupName, long groupId)
    {
        Dictionary<string, string> additionalInfo = BuildRequestContactInfo(request.RuleContactName, request.RuleContactId, request.RequestorName, request.RequestorId);
        additionalInfo[AdditionalInfoKeys.GrpName] = groupName;
        additionalInfo[AdditionalInfoKeys.GroupId] = groupId.ToString(CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(additionalInfo);
    }

    /// <summary>
    /// Builds the shared request/contact metadata block used by multiple ticket payloads.
    /// </summary>
    private static Dictionary<string, string> BuildRequestContactInfo(string? requestContactName, string? requestContactId, string? requestorName, string? requestorId)
    {
        Dictionary<string, string> additionalInfo = new();
        if (!string.IsNullOrWhiteSpace(requestContactName))
        {
            additionalInfo[AdditionalInfoKeys.RequestContactName] = requestContactName;
        }
        if (!string.IsNullOrWhiteSpace(requestContactId))
        {
            additionalInfo[AdditionalInfoKeys.RequestContactId] = requestContactId;
        }
        if (!string.IsNullOrWhiteSpace(requestorName))
        {
            additionalInfo[AdditionalInfoKeys.RequestorName] = requestorName;
        }
        if (!string.IsNullOrWhiteSpace(requestorId))
        {
            additionalInfo[AdditionalInfoKeys.RequestorId] = requestorId;
        }

        return additionalInfo;
    }

    /// <summary>
    /// Adds an entity to the request index and rejects duplicate ids.
    /// </summary>
    private static void AddEntity(Dictionary<long, CreateRequestEntity> entities, long id, CreateRequestEntity entity)
    {
        if (entities.TryAdd(id, entity))
        {
            return;
        }

        throw new ArgumentException($"Duplicate request object id {id}.");
    }

    /// <summary>
    /// Looks up a request entity or fails with a readable error.
    /// </summary>
    private static CreateRequestEntity GetEntity(Dictionary<long, CreateRequestEntity> entities, long id)
    {
        if (entities.TryGetValue(id, out CreateRequestEntity? entity))
        {
            return entity;
        }

        throw new ArgumentException($"Unknown request object id {id}.");
    }

    /// <summary>
    /// Persists the created ticket through the workflow save path so request actions are executed consistently.
    /// </summary>
    /// <param name="ticket">Ticket to persist.</param>
    /// <param name="phase">Workflow phase the ticket is created in.</param>
    /// <param name="callerName">Login name of the authenticated caller, empty for unauthenticated internal callers.</param>
    /// <param name="changerId">Database id of the authenticated caller, null for unauthenticated internal callers.</param>
    private async Task<WfTicket> SaveTicketAsync(WfTicket ticket, WorkflowPhases phase, string? callerName, int? changerId)
    {
        using UserConfig userConfig = CreateWorkflowUserConfig(callerName);
        WfHandler wfHandler = new(userConfig, apiConnection, phase, (List<UserGroup>?)null) { SystemContext = true, ChangerId = changerId };
        if (!await wfHandler.InitForActionExecution() || wfHandler.ActionHandler == null)
        {
            throw new InvalidOperationException($"Could not initialize workflow actions for request ticket creation in phase {phase}.");
        }

        WfDbAccess dbAccess = new((_, _, _, _) => { }, userConfig, apiConnection, wfHandler.ActionHandler, true, phase, false) { ChangerId = changerId };

        WfTicket createdTicket = await dbAccess.AddTicketToDb(ticket);

        long ticketId = createdTicket.Id;
        if (ticketId <= 0)
        {
            throw new InvalidOperationException("Could not create the request ticket.");
        }

        return createdTicket;
    }

    /// <summary>
    /// Builds the workflow config used to save a ticket. It carries the global settings like every other
    /// middleware entry point, plus the login name of the authenticated caller so the change history names
    /// that caller instead of the middleware server. It is created per request because the name differs
    /// between concurrent callers.
    /// </summary>
    /// <param name="callerName">Login name of the authenticated caller, empty for unauthenticated internal callers.</param>
    private UserConfig CreateWorkflowUserConfig(string? callerName)
    {
        UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, globalConfig.DefaultLanguage);
        userConfig.User.Name = callerName ?? "";
        return userConfig;
    }

    /// <summary>
    /// Builds the approval that accompanies each task.
    /// </summary>
    private static WfApproval BuildApproval(int stateId)
    {
        return new WfApproval
        {
            StateId = stateId,
            InitialApproval = true
        };
    }

    /// <summary>
    /// Serializes the metadata we want to keep alongside the ticket task.
    /// </summary>
    private static string BuildAdditionalInfo(string requestContactName, string requestContactId, string requestorName, string requestorId,
        CreateRequestEntity? timeEntity)
    {
        Dictionary<string, string> additionalInfo = BuildRequestContactInfo(requestContactName, requestContactId, requestorName, requestorId);
        if (timeEntity != null)
        {
            additionalInfo[AdditionalInfoKeys.TimeObjectId] = timeEntity.Id.ToString(CultureInfo.InvariantCulture);
        }
        return JsonSerializer.Serialize(additionalInfo);
    }

    /// <summary>
    /// Parses a request entity identifier and preserves negative temporary ids.
    /// </summary>
    private static long ParseEntityId(string value, string entityType)
    {
        if (long.TryParse(value, out long id) && id != 0)
        {
            return ParseLocalEntityId(id, entityType);
        }

        throw new ArgumentException($"The {entityType} id '{value}' must be a non-zero integer.");
    }

    private static long ParseLocalEntityId(long id, string entityType)
    {
        if (id < 0)
        {
            return id;
        }

        throw new ArgumentException($"The {entityType} id '{id}' must be negative because positive ids reference existing Flow objects.");
    }

    private sealed record CreateRequestEntity(
        long Id,
        CreateRequestEntityKind Kind,
        string DisplayName,
        string? IpStart = null,
        string? IpEnd = null,
        int? ProtocolId = null,
        int? PortStart = null,
        int? PortEnd = null,
        DateTime? TimeStart = null,
        DateTime? TimeEnd = null)
    {
        public static CreateRequestEntity FromAddressObject(long id, CreateRequestRequest.CreateAddressObjectRequest request)
        {
            return new CreateRequestEntity(
                id,
                CreateRequestEntityKind.AddressObject,
                request.Name,
                request.IpStart,
                request.IpEnd);
        }

        public static CreateRequestEntity FromAddressGroup(CreateRequestRequest.CreateAddressGroupRequest request)
        {
            return new CreateRequestEntity(request.Id, CreateRequestEntityKind.AddressGroup, request.Name);
        }

        public static CreateRequestEntity FromServiceObject(long id, CreateRequestRequest.CreateServiceObjectRequest request, Dictionary<string, int> protocolIds)
        {
            int protocolId = ResolveProtocolId(request.Protocol, protocolIds, request.PortStart, request.PortEnd);
            return new CreateRequestEntity(
                id,
                CreateRequestEntityKind.ServiceObject,
                request.Name,
                ProtocolId: protocolId,
                PortStart: request.PortStart,
                PortEnd: request.PortEnd);
        }

        public static CreateRequestEntity FromServiceGroup(CreateRequestRequest.CreateServiceGroupRequest request)
        {
            return new CreateRequestEntity(request.Id, CreateRequestEntityKind.ServiceGroup, request.Name);
        }

        public static CreateRequestEntity FromTimeObject(long id, CreateRequestRequest.CreateTimeObjectRequest request)
        {
            DateTime? startTime = ParseDateTime(request.StartTime, "startTime");
            DateTime? endTime = ParseDateTime(request.EndTime, "endTime");
            return new CreateRequestEntity(
                id,
                CreateRequestEntityKind.TimeObject,
                request.Name,
                TimeStart: startTime,
                TimeEnd: endTime);
        }

        public static CreateRequestEntity FromFlowTimeObject(FlowTimeObject flowObject)
        {
            return new CreateRequestEntity(
                flowObject.Id,
                CreateRequestEntityKind.TimeObject,
                flowObject.Name,
                TimeStart: flowObject.StartTime,
                TimeEnd: flowObject.EndTime);
        }

        private static DateTime? ParseDateTime(string value, string fieldName)
        {
            try
            {
                DateTime parsedDateTime = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (parsedDateTime.Kind == DateTimeKind.Unspecified)
                {
                    TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(parsedDateTime);
                    Log.WriteWarning(
                        "Flow Request",
                        $"Time object {fieldName} '{value}' has no timezone offset. Interpreting it as middleware local time with UTC offset {utcOffset}.");
                }

                return parsedDateTime;
            }
            catch (FormatException exception)
            {
                throw new ArgumentException($"The time object {fieldName} '{value}' must be a valid date/time value.", fieldName, exception);
            }
        }

        private static int ResolveProtocolId(string protocol, Dictionary<string, int> protocolIds, int? portStart, int? portEnd)
        {
            if (int.TryParse(protocol, out int protocolId))
            {
                return ValidateResolvedProtocolId(protocol, protocolId, protocolIds.ContainsValue(protocolId), portStart, portEnd);
            }

            if (protocolIds.TryGetValue(protocol, out protocolId))
            {
                return ValidateResolvedProtocolId(protocol, protocolId, true, portStart, portEnd);
            }

            throw new ArgumentException($"The service object protocol '{protocol}' must match a configured STM protocol name or id.");
        }

        private static int ValidateResolvedProtocolId(string protocol, int protocolId, bool isConfigured, int? portStart, int? portEnd)
        {
            if (isConfigured && protocolId >= 0)
            {
                ValidatePortRange(protocol, portStart, portEnd);
                return protocolId;
            }

            bool isCanonicalAnyIpProtocol = isConfigured && protocolId == GlobalConst.kAnyIpProtocolId && portStart is null && portEnd is null;
            if (isCanonicalAnyIpProtocol)
            {
                return protocolId;
            }

            throw new ArgumentException($"The service object protocol '{protocol}' must match a non-negative configured STM protocol name or id, or be the canonical any-IP-protocol service without ports.");
        }

        /// <summary>
        /// Rejects a 'portEnd' without a 'portStart': it cannot be resolved to a deterministic
        /// service hash and would otherwise be persisted as an internally inconsistent service.
        /// A 'portStart' without a 'portEnd' is a valid single-port shorthand and is left as-is.
        /// </summary>
        private static void ValidatePortRange(string protocol, int? portStart, int? portEnd)
        {
            if (portStart is null && portEnd is not null)
            {
                throw new ArgumentException($"The service object protocol '{protocol}' has a 'portEnd' value without a 'portStart' value.");
            }
        }
    }

    private enum CreateRequestEntityKind
    {
        AddressObject,
        AddressGroup,
        ServiceObject,
        ServiceGroup,
        TimeObject
    }

    /// <summary>
    /// Loads workflow state names.
    /// </summary>
    private async Task<WfStateDict> GetStateDictAsync()
    {
        WfStateDict loadedStateDict = new();
        await loadedStateDict.Init(apiConnection);
        return loadedStateDict;
    }

    /// <summary>
    /// Builds the public status string for a workflow request.
    /// </summary>
    private async Task<string> BuildRequestStatusAsync(int stateId, bool tolerateExternalStateErrors)
    {
        WfStateDict states = await GetStateDictAsync();
        string status = states.GetName(stateId);
        ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
        if (extStateResponse.HasErrors || extStateResponse.Result == null)
        {
            if (tolerateExternalStateErrors)
            {
                return status;
            }

            throw new InvalidOperationException("Could not fetch external workflow states.");
        }

        string? mappedStatus = ExtStateHandler.GetPreferredExternalStateName(extStateResponse.Result, stateId, true);
        if (!string.IsNullOrWhiteSpace(mappedStatus))
        {
            status = mappedStatus;
        }

        return status;
    }

    /// <summary>
    /// Returns the newest non-empty ticket-level comment text.
    /// </summary>
    private static string GetLatestTicketComment(WfTicket ticket)
    {
        return ticket.Comments?
            .Where(comment => comment?.Comment != null && !string.IsNullOrWhiteSpace(comment.Comment.CommentText))
            .OrderByDescending(comment => comment!.Comment.CreationDate)
            .Select(comment => comment!.Comment.CommentText)
            .FirstOrDefault() ?? string.Empty;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        configSubscription?.Dispose();
    }
}
