using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
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
/// Provides workflow ticket data for workflow REST endpoints.
/// </summary>
public sealed class WorkflowTicketService : IDisposable
{
    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;
    private readonly WorkflowTicketFlowReferenceService flowReferenceService;
    private readonly ApiSubscription? configSubscription;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public WorkflowTicketService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
        flowReferenceService = new WorkflowTicketFlowReferenceService(apiConnection);
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
    public async Task<CreateTicketResponse> CreateTicketAsync(CreateTicketRequest request, int requesterId, string? callerName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateTicket(request);
        if (requesterId <= 0)
        {
            throw new ArgumentException("'requesterId' must be a positive integer.");
        }

        (WorkflowPhases ticketPhase, int ticketStateId) = await ResolveInitialRequestPhaseAndStateAsync();
        Dictionary<int, FwoOwner> ownersById = await ResolveOwnersAsync();
        Dictionary<string, int> ruleActionIds = await ResolveRuleActionIdsAsync();
        Dictionary<string, int> protocolIds = await ResolveProtocolIdsAsync();
        FlowReferenceCatalog flowReferences = await flowReferenceService.ResolveAsync(request);
        WfTicket ticket = BuildTicket(request, ticketStateId, requesterId, ownersById, ruleActionIds, protocolIds, flowReferences);
        // requesterId is the id of the authenticated caller, so it is also the changer - but only when that
        // caller is named. An internal caller supplies a requester without being the user who made the change,
        // and attributing the change history entry to that requester would be wrong.
        int? changerId = string.IsNullOrWhiteSpace(callerName) ? null : requesterId;
        ticket = await SaveTicketAsync(ticket, ticketPhase, callerName, changerId);
        string status = await BuildTicketStatusAsync(ticket.StateId, tolerateExternalStateErrors: true);

        return new CreateTicketResponse
        {
            Status = status,
            TicketId = ticket.Id
        };
    }

    /// <summary>
    /// Returns the workflow ticket status and latest ticket comment.
    /// </summary>
    public async Task<GetTicketStatusResponse?> GetTicketStatusAsync(long ticketId)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        return new GetTicketStatusResponse
        {
            Status = await BuildTicketStatusAsync(ticket.StateId, tolerateExternalStateErrors: false),
            StatusComment = GetLatestTicketComment(ticket)
        };
    }

    /// <summary>
    /// Returns a workflow ticket with all of its tasks, approvals, implementation tasks, elements,
    /// owners and comments.
    /// </summary>
    /// <param name="ticketId">Database id of the workflow ticket.</param>
    /// <param name="filter">Optional request task filter; null returns every task.</param>
    /// <returns>The ticket, or null when no workflow ticket with that id exists.</returns>
    /// <remarks>
    /// The filter restricts the returned tasks only: a ticket whose tasks the filter excludes is
    /// still returned, with an empty task list, so it cannot be mistaken for a missing ticket.
    /// </remarks>
    public async Task<GetTicketResponse?> GetTicketAsync(long ticketId, TicketTaskFilter? filter)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        WfStateDict states = await GetStateDictAsync();
        string status = await BuildTicketStatusAsync(ticket.StateId, states, tolerateExternalStateErrors: false);
        return TicketResponseMapper.Map(ticket, states, status, filter);
    }

    /// <summary>
    /// Validates the create-ticket payload before the ticket is built.
    /// </summary>
    private static void ValidateCreateTicket(CreateTicketRequest request)
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

    private sealed record RuleTaskLookups(Dictionary<int, FwoOwner> OwnersById, Dictionary<string, int> RuleActionIds);

    /// <summary>
    /// Builds the ticket object that is persisted through the existing whole-ticket insert path.
    /// </summary>
    private WfTicket BuildTicket(CreateTicketRequest request, int ticketStateId, int requesterId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds,
        Dictionary<string, int> protocolIds, FlowReferenceCatalog flowReferences)
    {
        Dictionary<long, WorkflowTicketEntity> entities = BuildEntityIndex(request, protocolIds);
        List<WfReqTask> tasks = [];
        int taskNumber = 1;

        tasks.AddRange(BuildGroupTasks(request, entities, ticketStateId, flowReferences, ref taskNumber));
        tasks.AddRange(BuildRuleTasks(request, entities, ticketStateId, ownersById, ruleActionIds, flowReferences, ref taskNumber));
        CreateRequestTaskSortConfig sortConfig = CreateRequestTaskSortConfig.Parse(globalConfig.ReqCreateRequestTaskSortConfig);
        tasks = CreateRequestTaskSorter.OrderForSave(tasks, request.Options.SortTasks ?? false, sortConfig);

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
    private static UiUser BuildRequester(CreateTicketRequest request, int requesterId)
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
    private static string BuildRequestReason(CreateTicketRequest request)
    {
        return $"{request.RuleContactName} ({request.RuleContactId})";
    }

    /// <summary>
    /// Builds all group/entity lookup entries and checks for duplicate ids.
    /// </summary>
    private static Dictionary<long, WorkflowTicketEntity> BuildEntityIndex(CreateTicketRequest request, Dictionary<string, int> protocolIds)
    {
        Dictionary<long, WorkflowTicketEntity> entities = [];

        foreach (CreateTicketRequest.CreateAddressObjectRequest addressObject in request.AddressObjects)
        {
            long entityId = ParseLocalEntityId(addressObject.Id, "address object");
            AddEntity(entities, entityId, WorkflowTicketEntity.FromAddressObject(entityId, addressObject));
        }

        foreach (CreateTicketRequest.CreateServiceObjectRequest serviceObject in request.ServiceObjects)
        {
            long entityId = ParseLocalEntityId(serviceObject.Id, "service object");
            AddEntity(entities, entityId, WorkflowTicketEntity.FromServiceObject(entityId, serviceObject, protocolIds));
        }

        foreach (CreateTicketRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            AddEntity(
                entities,
                ParseLocalEntityId(addressGroup.Id, "address group"),
                WorkflowTicketEntity.FromAddressGroup(addressGroup));
        }

        foreach (CreateTicketRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            AddEntity(
                entities,
                ParseLocalEntityId(serviceGroup.Id, "service group"),
                WorkflowTicketEntity.FromServiceGroup(serviceGroup));
        }

        foreach (CreateTicketRequest.CreateTimeObjectRequest timeObject in request.TimeObjects)
        {
            long entityId = ParseLocalEntityId(timeObject.Id, "time object");
            AddEntity(entities, entityId, WorkflowTicketEntity.FromTimeObject(entityId, timeObject));
        }

        return entities;
    }

    /// <summary>
    /// Builds the access tasks for the request rules.
    /// </summary>
    private static List<WfReqTask> BuildRuleTasks(CreateTicketRequest request, Dictionary<long, WorkflowTicketEntity> entities,
        int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds, FlowReferenceCatalog flowReferences, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        RuleTaskLookups lookups = new(ownersById, ruleActionIds);
        foreach (CreateTicketRequest.CreateTicketRuleRequest rule in request.Rules)
        {
            tasks.Add(BuildRuleTask(request, rule, entities, ticketStateId, lookups, flowReferences, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Builds ticket tasks that create object groups.
    /// </summary>
    private static List<WfReqTask> BuildGroupTasks(CreateTicketRequest request, Dictionary<long, WorkflowTicketEntity> entities,
        int ticketStateId, FlowReferenceCatalog flowReferences, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        foreach (CreateTicketRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            tasks.Add(BuildNetworkGroupTask(request, addressGroup, entities, ticketStateId, flowReferences, taskNumber++));
        }

        foreach (CreateTicketRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            tasks.Add(BuildServiceGroupTask(request, serviceGroup, entities, ticketStateId, flowReferences, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Creates a task for an address group.
    /// </summary>
    private static WfReqTask BuildNetworkGroupTask(CreateTicketRequest request, CreateTicketRequest.CreateAddressGroupRequest group,
        Dictionary<long, WorkflowTicketEntity> entities, int ticketStateId, FlowReferenceCatalog flowReferences, int taskNumber)
    {
        WorkflowTicketEntity groupEntity = entities[group.Id];
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
    private static WfReqTask BuildServiceGroupTask(CreateTicketRequest request, CreateTicketRequest.CreateServiceGroupRequest group,
        Dictionary<long, WorkflowTicketEntity> entities, int ticketStateId, FlowReferenceCatalog flowReferences, int taskNumber)
    {
        WorkflowTicketEntity groupEntity = entities[group.Id];
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
    private static WfReqTask BuildRuleTask(CreateTicketRequest request, CreateTicketRequest.CreateTicketRuleRequest rule,
        Dictionary<long, WorkflowTicketEntity> entities, int ticketStateId, RuleTaskLookups lookups,
        FlowReferenceCatalog flowReferences, int taskNumber)
    {
        List<WfReqElement> elements =
        [
            .. BuildReferencedElements(rule.SourceObjects, entities, ElemFieldType.source, WorkflowTicketEntityKind.AddressObject, flowReferences),
            .. BuildReferencedElements(rule.SourceGroups, entities, ElemFieldType.source, WorkflowTicketEntityKind.AddressGroup, flowReferences),
            .. BuildReferencedElements(rule.DestinationObjects, entities, ElemFieldType.destination, WorkflowTicketEntityKind.AddressObject, flowReferences),
            .. BuildReferencedElements(rule.DestinationGroups, entities, ElemFieldType.destination, WorkflowTicketEntityKind.AddressGroup, flowReferences),
            .. BuildReferencedElements(rule.ServiceObjects, entities, ElemFieldType.service, WorkflowTicketEntityKind.ServiceObject, flowReferences),
            .. BuildReferencedElements(rule.ServiceGroups, entities, ElemFieldType.service, WorkflowTicketEntityKind.ServiceGroup, flowReferences)
        ];

        int ruleActionId = ResolveRuleActionId(rule.Action, lookups.RuleActionIds);
        FwoOwner? taskOwner = ResolveRuleOwner(rule.OwnerId, lookups.OwnersById);

        WorkflowTicketEntity? timeEntity = ResolveTimeObject(rule.TimeObjectId, entities, flowReferences);

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
    private static WorkflowTicketEntity? ResolveTimeObject(long timeObjectId, Dictionary<long, WorkflowTicketEntity> entities, FlowReferenceCatalog flowReferences)
    {
        if (timeObjectId == 0)
        {
            return null;
        }

        if (timeObjectId > 0)
        {
            if (flowReferences.TimeObjects.TryGetValue(timeObjectId, out FlowTimeObject? flowTimeObject))
            {
                return WorkflowTicketEntity.FromFlowTimeObject(flowTimeObject);
            }

            throw new ArgumentException($"Unknown Flow time object id {timeObjectId}.");
        }

        if (!entities.TryGetValue(timeObjectId, out WorkflowTicketEntity? entity) || entity.Kind != WorkflowTicketEntityKind.TimeObject)
        {
            throw new ArgumentException($"Time object id {timeObjectId} must reference a time object.");
        }

        return entity;
    }

    /// <summary>
    /// Builds elements for rule references.
    /// </summary>
    private static IEnumerable<WfReqElement> BuildReferencedElements(IEnumerable<long> references, Dictionary<long, WorkflowTicketEntity> entities, ElemFieldType field,
        WorkflowTicketEntityKind expectedKind, FlowReferenceCatalog flowReferences)
    {
        foreach (long reference in references)
        {
            yield return BuildReferencedElement(reference, entities, field, expectedKind, flowReferences);
        }
    }

    /// <summary>
    /// Converts a single reference into a workflow element.
    /// </summary>
    private static WfReqElement BuildReferencedElement(long reference, Dictionary<long, WorkflowTicketEntity> entities, ElemFieldType field,
        WorkflowTicketEntityKind expectedKind, FlowReferenceCatalog flowReferences)
    {
        if (reference > 0)
        {
            return expectedKind is WorkflowTicketEntityKind.AddressGroup or WorkflowTicketEntityKind.ServiceGroup
                ? flowReferences.BuildGroupElement(reference, field)
                : flowReferences.BuildObjectElement(reference, field);
        }

        WorkflowTicketEntity entity = GetEntity(entities, reference);
        if (entity.Kind != expectedKind)
        {
            throw new ArgumentException($"Reference id {reference} in field '{field}' must reference a {expectedKind.ToString().ToLowerInvariant()}.");
        }

        if (field == ElemFieldType.service)
        {
            return entity.Kind switch
            {
                WorkflowTicketEntityKind.ServiceObject => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    Port = entity.PortStart,
                    PortEnd = entity.PortEnd,
                    ProtoId = entity.ProtocolId
                },
                WorkflowTicketEntityKind.ServiceGroup => new WfReqElement
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
                WorkflowTicketEntityKind.AddressObject => new WfReqElement
                {
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = entity.DisplayName,
                    IpString = entity.IpStart,
                    IpEnd = entity.IpEnd
                },
                WorkflowTicketEntityKind.AddressGroup => new WfReqElement
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
    private static WfReqElement BuildGroupMemberElement(long memberId, Dictionary<long, WorkflowTicketEntity> entities, ElemFieldType field,
        FlowReferenceCatalog flowReferences)
    {
        if (memberId > 0)
        {
            return flowReferences.BuildObjectElement(memberId, field);
        }

        WorkflowTicketEntity entity = GetEntity(entities, memberId);
        WorkflowTicketEntityKind expectedKind = field == ElemFieldType.service
            ? WorkflowTicketEntityKind.ServiceObject
            : WorkflowTicketEntityKind.AddressObject;
        if (entity.Kind != expectedKind)
        {
            throw new ArgumentException($"Member id {memberId} must reference a {expectedKind.ToString().ToLowerInvariant()}.");
        }

        return entity.Kind switch
        {
            WorkflowTicketEntityKind.AddressObject => new WfReqElement
            {
                Field = field.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Name = entity.DisplayName,
                IpString = entity.IpStart,
                IpEnd = entity.IpEnd
            },
            WorkflowTicketEntityKind.ServiceObject => new WfReqElement
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
    private static string BuildGroupAdditionalInfo(CreateTicketRequest request, string groupName, long groupId)
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
    private static void AddEntity(Dictionary<long, WorkflowTicketEntity> entities, long id, WorkflowTicketEntity entity)
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
    private static WorkflowTicketEntity GetEntity(Dictionary<long, WorkflowTicketEntity> entities, long id)
    {
        if (entities.TryGetValue(id, out WorkflowTicketEntity? entity))
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
        WorkflowTicketEntity? timeEntity)
    {
        Dictionary<string, string> additionalInfo = BuildRequestContactInfo(requestContactName, requestContactId, requestorName, requestorId);
        if (timeEntity != null)
        {
            additionalInfo[AdditionalInfoKeys.TimeObjectId] = timeEntity.Id.ToString(CultureInfo.InvariantCulture);
        }
        return JsonSerializer.Serialize(additionalInfo);
    }

    /// <summary>
    /// Validates a request-local entity identifier.
    /// </summary>
    private static long ParseLocalEntityId(long id, string entityType)
    {
        if (id < 0)
        {
            return id;
        }

        throw new ArgumentException($"The {entityType} id '{id}' must be a negative, non-zero integer because positive ids reference existing Flow objects.");
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
    private async Task<string> BuildTicketStatusAsync(int stateId, bool tolerateExternalStateErrors)
    {
        return await BuildTicketStatusAsync(stateId, await GetStateDictAsync(), tolerateExternalStateErrors);
    }

    /// <summary>
    /// Builds the public status string for a workflow request from already loaded state names.
    /// </summary>
    private async Task<string> BuildTicketStatusAsync(int stateId, WfStateDict states, bool tolerateExternalStateErrors)
    {
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
