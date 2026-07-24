using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;
using System.Globalization;
using System.Text.Json;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides request workflow data for flow request REST endpoints.
/// </summary>
public sealed class FlowRequestService
{
    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowRequestService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
    }

    /// <summary>
    /// Creates a new workflow ticket from the high-level request payload.
    /// </summary>
    public async Task<CreateRequestResponse> CreateRequestAsync(CreateRequestRequest request, int requesterId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateRequest(request);
        if (requesterId <= 0)
        {
            throw new ArgumentException("'requesterId' must be a positive integer.");
        }

        int ticketStateId = await ResolveInitialRequestStateIdAsync();
        Dictionary<int, FwoOwner> ownersById = await ResolveOwnersAsync();
        Dictionary<string, int> ruleActionIds = await ResolveRuleActionIdsAsync();
        Dictionary<string, int> protocolIds = await ResolveProtocolIdsAsync();
        WfTicket ticket = BuildTicket(request, ticketStateId, requesterId, ownersById, ruleActionIds, protocolIds);
        ticket = await SaveTicketAsync(ticket);
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
    /// Resolves the initial state id for a newly created request ticket.
    /// </summary>
    private async Task<int> ResolveInitialRequestStateIdAsync()
    {
        List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates) ?? [];
        int configuredStateId = globalConfig.ReqApiTicketInitialStateId;
        if (configuredStateId >= 0)
        {
            if (states.Any(state => state.Id == configuredStateId))
            {
                return configuredStateId;
            }

            throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} does not exist in the current state list.");
        }

        StateMatrixConfigurationSnapshot stateMatrix = await StateMatrixConfigurationRepository.Load(apiConnection, WfTaskType.master);
        return stateMatrix.Matrices[WorkflowPhases.request].LowestInputState;
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

    /// <summary>
    /// Builds the ticket object that is persisted through the existing whole-ticket insert path.
    /// </summary>
    private static WfTicket BuildTicket(CreateRequestRequest request, int ticketStateId, int requesterId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds,
        Dictionary<string, int> protocolIds)
    {
        Dictionary<int, CreateRequestEntity> entities = BuildEntityIndex(request, protocolIds);
        List<WfReqTask> tasks = [];
        int taskNumber = 1;

        tasks.AddRange(BuildGroupTasks(request, entities, ticketStateId, ref taskNumber));
        tasks.AddRange(BuildRuleTasks(request, entities, ticketStateId, ownersById, ruleActionIds, ref taskNumber));

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
    private static Dictionary<int, CreateRequestEntity> BuildEntityIndex(CreateRequestRequest request, Dictionary<string, int> protocolIds)
    {
        Dictionary<int, CreateRequestEntity> entities = [];

        foreach (CreateRequestRequest.CreateAddressObjectRequest addressObject in request.AddressObjects)
        {
            int entityId = ParseEntityId(addressObject.Id, "address object");
            AddEntity(entities, entityId, CreateRequestEntity.FromAddressObject(entityId, addressObject));
        }

        foreach (CreateRequestRequest.CreateServiceObjectRequest serviceObject in request.ServiceObjects)
        {
            int entityId = ParseEntityId(serviceObject.Id, "service object");
            AddEntity(entities, entityId, CreateRequestEntity.FromServiceObject(entityId, serviceObject, protocolIds));
        }

        foreach (CreateRequestRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            AddEntity(entities, addressGroup.Id, CreateRequestEntity.FromAddressGroup(addressGroup));
        }

        foreach (CreateRequestRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            AddEntity(entities, serviceGroup.Id, CreateRequestEntity.FromServiceGroup(serviceGroup));
        }

        foreach (CreateRequestRequest.CreateTimeObjectRequest timeObject in request.TimeObjects)
        {
            int entityId = ParseEntityId(timeObject.Id, "time object");
            AddEntity(entities, entityId, CreateRequestEntity.FromTimeObject(entityId, timeObject));
        }

        return entities;
    }

    /// <summary>
    /// Builds the access tasks for the request rules.
    /// </summary>
    private static List<WfReqTask> BuildRuleTasks(CreateRequestRequest request, Dictionary<int, CreateRequestEntity> entities,
        int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        foreach (CreateRequestRequest.CreateRequestRuleRequest rule in request.Rules)
        {
            tasks.Add(BuildRuleTask(request, rule, entities, ticketStateId, ownersById, ruleActionIds, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Builds ticket tasks that create object groups.
    /// </summary>
    private static List<WfReqTask> BuildGroupTasks(CreateRequestRequest request, Dictionary<int, CreateRequestEntity> entities,
        int ticketStateId, ref int taskNumber)
    {
        List<WfReqTask> tasks = [];
        foreach (CreateRequestRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            tasks.Add(BuildNetworkGroupTask(request, addressGroup, entities, ticketStateId, taskNumber++));
        }

        foreach (CreateRequestRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            tasks.Add(BuildServiceGroupTask(request, serviceGroup, entities, ticketStateId, taskNumber++));
        }
        return tasks;
    }

    /// <summary>
    /// Creates a task for an address group.
    /// </summary>
    private static WfReqTask BuildNetworkGroupTask(CreateRequestRequest request, CreateRequestRequest.CreateAddressGroupRequest group,
        Dictionary<int, CreateRequestEntity> entities, int ticketStateId, int taskNumber)
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
            Elements = [.. group.MemberIds.Select(memberId => BuildGroupMemberElement(memberId, entities, ElemFieldType.source))],
            Approvals = [BuildApproval(ticketStateId)],
            Locked = true
        };
    }

    /// <summary>
    /// Creates a task for a service group.
    /// </summary>
    private static WfReqTask BuildServiceGroupTask(CreateRequestRequest request, CreateRequestRequest.CreateServiceGroupRequest group,
        Dictionary<int, CreateRequestEntity> entities, int ticketStateId, int taskNumber)
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
            Elements = [.. group.MemberIds.Select(memberId => BuildGroupMemberElement(memberId, entities, ElemFieldType.service))],
            Approvals = [BuildApproval(ticketStateId)],
            Locked = true
        };
    }

    /// <summary>
    /// Creates an access task for one request rule.
    /// </summary>
    private static WfReqTask BuildRuleTask(CreateRequestRequest request, CreateRequestRequest.CreateRequestRuleRequest rule,
        Dictionary<int, CreateRequestEntity> entities, int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds, int taskNumber)
    {
        List<WfReqElement> elements =
        [
            .. BuildReferencedElements(rule.SourceObjects, entities, ElemFieldType.source),
            .. BuildReferencedElements(rule.DestinationObjects, entities, ElemFieldType.destination),
            .. BuildReferencedElements(rule.ServiceObjects, entities, ElemFieldType.service)
        ];

        int ruleActionId = ResolveRuleActionId(rule.Action, ruleActionIds);
        FwoOwner? taskOwner = ResolveRuleOwner(rule.OwnerId, ownersById);

        CreateRequestEntity? timeEntity = ResolveTimeObject(rule.TimeObjectId, entities);

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
    private static CreateRequestEntity? ResolveTimeObject(int timeObjectId, Dictionary<int, CreateRequestEntity> entities)
    {
        if (timeObjectId == 0)
        {
            return null;
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
    private static IEnumerable<WfReqElement> BuildReferencedElements(IEnumerable<int> references, Dictionary<int, CreateRequestEntity> entities, ElemFieldType field)
    {
        foreach (int reference in references)
        {
            yield return BuildReferencedElement(reference, entities, field);
        }
    }

    /// <summary>
    /// Converts a single reference into a workflow element.
    /// </summary>
    private static WfReqElement BuildReferencedElement(int reference, Dictionary<int, CreateRequestEntity> entities, ElemFieldType field)
    {
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
    private static WfReqElement BuildGroupMemberElement(int memberId, Dictionary<int, CreateRequestEntity> entities, ElemFieldType field)
    {
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
    private static string BuildGroupAdditionalInfo(CreateRequestRequest request, string groupName, int groupId)
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
    private static void AddEntity(Dictionary<int, CreateRequestEntity> entities, int id, CreateRequestEntity entity)
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
    private static CreateRequestEntity GetEntity(Dictionary<int, CreateRequestEntity> entities, int id)
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
    private async Task<WfTicket> SaveTicketAsync(WfTicket ticket)
    {
        using UserConfig userConfig = new();
        WfHandler wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, (List<UserGroup>?)null);
        ActionHandler actionHandler = new(apiConnection, wfHandler, null, true);
        await actionHandler.Init();
        WfDbAccess dbAccess = new((_, _, _, _) => { }, userConfig, apiConnection, actionHandler, true);

        WfTicket createdTicket = await dbAccess.AddTicketToDb(ticket);
        long ticketId = createdTicket.Id;
        if (ticketId <= 0)
        {
            throw new InvalidOperationException("Could not create the request ticket.");
        }

        return createdTicket;
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
    private static int ParseEntityId(string value, string entityType)
    {
        if (int.TryParse(value, out int id) && id != 0)
        {
            return id;
        }

        throw new ArgumentException($"The {entityType} id '{value}' must be a non-zero integer.");
    }

    private sealed record CreateRequestEntity(
        int Id,
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
        public static CreateRequestEntity FromAddressObject(int id, CreateRequestRequest.CreateAddressObjectRequest request)
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

        public static CreateRequestEntity FromServiceObject(int id, CreateRequestRequest.CreateServiceObjectRequest request, Dictionary<string, int> protocolIds)
        {
            int protocolId = ResolveProtocolId(request.Protocol, protocolIds);
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

        public static CreateRequestEntity FromTimeObject(int id, CreateRequestRequest.CreateTimeObjectRequest request)
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

        private static DateTime? ParseDateTime(string value, string fieldName)
        {
            try
            {
                return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException($"The time object {fieldName} '{value}' must be a valid date/time value.", fieldName, exception);
            }
        }

        private static int ResolveProtocolId(string protocol, Dictionary<string, int> protocolIds)
        {
            if (int.TryParse(protocol, out int protocolId))
            {
                if (protocolId > 0 && protocolIds.ContainsValue(protocolId))
                {
                    return protocolId;
                }

                throw new ArgumentException($"The service object protocol '{protocol}' must match a configured STM protocol name or id.");
            }

            if (protocolIds.TryGetValue(protocol, out protocolId))
            {
                return protocolId;
            }

            throw new ArgumentException($"The service object protocol '{protocol}' must match a configured STM protocol name or id.");
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
}
