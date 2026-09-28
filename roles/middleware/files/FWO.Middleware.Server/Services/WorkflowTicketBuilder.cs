using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;
using System.Globalization;
using System.Text.Json;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Converts a validated create-ticket request into the workflow ticket model.
/// </summary>
internal sealed class WorkflowTicketBuilder
{
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new ticket builder.
    /// </summary>
    /// <param name="globalConfig">The live global configuration used for task ordering.</param>
    public WorkflowTicketBuilder(GlobalConfig globalConfig)
    {
        this.globalConfig = globalConfig;
    }

    /// <summary>
    /// Builds the ticket object that is persisted through the existing whole-ticket insert path.
    /// </summary>
    public WfTicket Build(CreateTicketRequest request, int ticketStateId, int requesterId, Dictionary<int, FwoOwner> ownersById,
        Dictionary<string, int> ruleActionIds, Dictionary<string, int> protocolIds, FlowReferenceCatalog flowReferences)
    {
        TicketBuildContext context = new();
        Dictionary<long, WorkflowTicketEntity> entities = BuildEntityIndex(request, protocolIds, context);
        List<WfReqTask> tasks = [];
        tasks.AddRange(BuildGroupTasks(request, entities, ticketStateId, flowReferences, context));
        tasks.AddRange(BuildRuleTasks(request, entities, ticketStateId, ownersById, ruleActionIds, flowReferences, context));
        if (context.ValidationErrors.Count > 0)
        {
            throw new CreateTicketValidationException(context.ValidationErrors);
        }
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

    private static string BuildRequestReason(CreateTicketRequest request)
    {
        return $"{request.RuleContactName} ({request.RuleContactId})";
    }

    /// <summary>
    /// Builds all group/entity lookup entries and checks for duplicate ids.
    /// </summary>
    private static Dictionary<long, WorkflowTicketEntity> BuildEntityIndex(CreateTicketRequest request,
        Dictionary<string, int> protocolIds, TicketBuildContext context)
    {
        Dictionary<long, WorkflowTicketEntity> entities = [];

        for (int index = 0; index < request.AddressObjects.Count; index++)
        {
            try
            {
                CreateTicketRequest.CreateAddressObjectRequest addressObject = request.AddressObjects[index];
                long entityId = ParseLocalEntityId(addressObject.Id, "address object");
                AddEntity(entities, entityId, WorkflowTicketEntity.FromAddressObject(entityId, addressObject));
            }
            catch (ArgumentException exception)
            {
                context.InvalidEntityIds.Add(request.AddressObjects[index].Id);
                AddValidationError(context.ValidationErrors, $"addressObjects[{index}]", exception);
            }
        }

        for (int index = 0; index < request.ServiceObjects.Count; index++)
        {
            try
            {
                CreateTicketRequest.CreateServiceObjectRequest serviceObject = request.ServiceObjects[index];
                long entityId = ParseLocalEntityId(serviceObject.Id, "service object");
                AddEntity(entities, entityId, WorkflowTicketEntity.FromServiceObject(entityId, serviceObject, protocolIds));
            }
            catch (ArgumentException exception)
            {
                context.InvalidEntityIds.Add(request.ServiceObjects[index].Id);
                AddValidationError(context.ValidationErrors, $"serviceObjects[{index}]", exception);
            }
        }

        for (int index = 0; index < request.AddressGroups.Count; index++)
        {
            try
            {
                CreateTicketRequest.CreateAddressGroupRequest addressGroup = request.AddressGroups[index];
                AddEntity(entities, ParseLocalEntityId(addressGroup.Id, "address group"), WorkflowTicketEntity.FromAddressGroup(addressGroup));
            }
            catch (ArgumentException exception)
            {
                context.InvalidEntityIds.Add(request.AddressGroups[index].Id);
                AddValidationError(context.ValidationErrors, $"addressGroups[{index}]", exception);
            }
        }

        for (int index = 0; index < request.ServiceGroups.Count; index++)
        {
            try
            {
                CreateTicketRequest.CreateServiceGroupRequest serviceGroup = request.ServiceGroups[index];
                AddEntity(entities, ParseLocalEntityId(serviceGroup.Id, "service group"), WorkflowTicketEntity.FromServiceGroup(serviceGroup));
            }
            catch (ArgumentException exception)
            {
                context.InvalidEntityIds.Add(request.ServiceGroups[index].Id);
                AddValidationError(context.ValidationErrors, $"serviceGroups[{index}]", exception);
            }
        }

        for (int index = 0; index < request.TimeObjects.Count; index++)
        {
            try
            {
                CreateTicketRequest.CreateTimeObjectRequest timeObject = request.TimeObjects[index];
                long entityId = ParseLocalEntityId(timeObject.Id, "time object");
                AddEntity(entities, entityId, WorkflowTicketEntity.FromTimeObject(entityId, timeObject));
            }
            catch (ArgumentException exception)
            {
                context.InvalidEntityIds.Add(request.TimeObjects[index].Id);
                AddValidationError(context.ValidationErrors, $"timeObjects[{index}]", exception);
            }
        }

        return entities;
    }

    private static List<WfReqTask> BuildRuleTasks(CreateTicketRequest request, Dictionary<long, WorkflowTicketEntity> entities,
        int ticketStateId, Dictionary<int, FwoOwner> ownersById, Dictionary<string, int> ruleActionIds,
        FlowReferenceCatalog flowReferences, TicketBuildContext context)
    {
        List<WfReqTask> tasks = [];
        RuleTaskLookups lookups = new(ownersById, ruleActionIds);
        for (int index = 0; index < request.Rules.Count; index++)
        {
            WorkflowTicketRuleTaskContext taskContext = new(entities, ticketStateId, lookups, flowReferences,
                context.TaskNumber++, index, context);
            WfReqTask? task = BuildRuleTask(request, request.Rules[index], taskContext);
            if (task != null)
            {
                tasks.Add(task);
            }
        }
        return tasks;
    }

    private static List<WfReqTask> BuildGroupTasks(CreateTicketRequest request, Dictionary<long, WorkflowTicketEntity> entities,
        int ticketStateId, FlowReferenceCatalog flowReferences, TicketBuildContext context)
    {
        List<WfReqTask> tasks = [];
        WorkflowTicketTaskContext taskContext = new(entities, ticketStateId, flowReferences, context);
        for (int index = 0; index < request.AddressGroups.Count; index++)
        {
            try
            {
                tasks.Add(BuildNetworkGroupTask(request, request.AddressGroups[index], taskContext, context.TaskNumber++,
                    $"addressGroups[{index}]"));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(context.ValidationErrors, $"addressGroups[{index}]", exception);
            }
        }

        for (int index = 0; index < request.ServiceGroups.Count; index++)
        {
            try
            {
                tasks.Add(BuildServiceGroupTask(request, request.ServiceGroups[index], taskContext, context.TaskNumber++,
                    $"serviceGroups[{index}]"));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(context.ValidationErrors, $"serviceGroups[{index}]", exception);
            }
        }
        return tasks;
    }

    private static WfReqTask BuildNetworkGroupTask(CreateTicketRequest request, CreateTicketRequest.CreateAddressGroupRequest group,
        WorkflowTicketTaskContext taskContext, int taskNumber, string groupPath)
    {
        WorkflowTicketEntity groupEntity = GetEntity(taskContext.Entities, group.Id);
        if (groupEntity.Kind != WorkflowTicketEntityKind.AddressGroup)
        {
            throw new ArgumentException($"Group id {group.Id} must reference an address group.");
        }
        return new WfReqTask
        {
            Title = groupEntity.DisplayName,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.group_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = taskContext.TicketStateId,
            AdditionalInfo = BuildGroupAdditionalInfo(request, groupEntity.DisplayName, group.Id),
            Elements = WorkflowTicketElementValidation.BuildGroupMemberElements(group.MemberIds,
                CreateElementContext(taskContext, $"{groupPath}.memberIds",
                    memberId => BuildGroupMemberElement(memberId, taskContext.Entities, ElemFieldType.source, taskContext.FlowReferences))),
            Approvals = [BuildApproval(taskContext.TicketStateId)],
            Locked = true
        };
    }

    private static WfReqTask BuildServiceGroupTask(CreateTicketRequest request, CreateTicketRequest.CreateServiceGroupRequest group,
        WorkflowTicketTaskContext taskContext, int taskNumber, string groupPath)
    {
        WorkflowTicketEntity groupEntity = GetEntity(taskContext.Entities, group.Id);
        if (groupEntity.Kind != WorkflowTicketEntityKind.ServiceGroup)
        {
            throw new ArgumentException($"Group id {group.Id} must reference a service group.");
        }
        return new WfReqTask
        {
            Title = groupEntity.DisplayName,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.group_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = taskContext.TicketStateId,
            AdditionalInfo = BuildGroupAdditionalInfo(request, groupEntity.DisplayName, group.Id),
            Elements = WorkflowTicketElementValidation.BuildGroupMemberElements(group.MemberIds,
                CreateElementContext(taskContext, $"{groupPath}.memberIds",
                    memberId => BuildGroupMemberElement(memberId, taskContext.Entities, ElemFieldType.service, taskContext.FlowReferences))),
            Approvals = [BuildApproval(taskContext.TicketStateId)],
            Locked = true
        };
    }

    private static WorkflowTicketElementContext CreateElementContext(WorkflowTicketTaskContext taskContext, string path,
        Func<long, WfReqElement> buildElement)
    {
        return new(taskContext.Validation.InvalidEntityIds, taskContext.Validation.ValidationErrors, path, buildElement);
    }

    private static WfReqTask? BuildRuleTask(CreateTicketRequest request, CreateTicketRequest.CreateTicketRuleRequest rule,
        WorkflowTicketRuleTaskContext taskContext)
    {
        int errorCount = taskContext.Validation.ValidationErrors.Count;
        string rulePath = $"rules[{taskContext.RuleIndex}]";
        List<WfReqElement> elements = [];
        AppendRuleReferences(elements, rule, taskContext, rulePath);

        int ruleActionId = 0;
        try
        {
            ruleActionId = ResolveRuleActionId(rule.Action, taskContext.Lookups.RuleActionIds);
        }
        catch (ArgumentException exception)
        {
            AddValidationError(taskContext.Validation.ValidationErrors, $"{rulePath}.action", exception);
        }

        FwoOwner? taskOwner = null;
        try
        {
            taskOwner = ResolveRuleOwner(rule.OwnerId, taskContext.Lookups.OwnersById);
        }
        catch (ArgumentException exception)
        {
            AddValidationError(taskContext.Validation.ValidationErrors, $"{rulePath}.ownerId", exception);
        }

        WorkflowTicketEntity? timeEntity = null;
        try
        {
            timeEntity = ResolveTimeObject(rule.TimeObjectId, taskContext.Entities, taskContext.FlowReferences);
        }
        catch (ArgumentException exception)
        {
            AddValidationError(taskContext.Validation.ValidationErrors, $"{rulePath}.timeObjectId", exception);
        }

        if (taskContext.Validation.ValidationErrors.Count > errorCount)
        {
            return null;
        }

        return new WfReqTask
        {
            Title = string.IsNullOrWhiteSpace(rule.Name) ? request.Title : rule.Name,
            TaskNumber = taskContext.TaskNumber,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = taskContext.TicketStateId,
            RuleAction = ruleActionId,
            Tracking = 1,
            Reason = rule.ViolationJustification,
            TargetBeginDate = timeEntity?.TimeStart,
            TargetEndDate = timeEntity?.TimeEnd,
            AdditionalInfo = BuildAdditionalInfo(request.RuleContactName, request.RuleContactId, request.RequestorName, request.RequestorId, timeEntity),
            Elements = elements,
            Approvals = [BuildApproval(taskContext.TicketStateId)],
            Owners = taskOwner == null ? [] : [new() { Owner = taskOwner }],
            Locked = true
        };
    }

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

    private static WorkflowTicketEntity? ResolveTimeObject(long timeObjectId, Dictionary<long, WorkflowTicketEntity> entities,
        FlowReferenceCatalog flowReferences)
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

    private static void AppendRuleReferences(List<WfReqElement> elements, CreateTicketRequest.CreateTicketRuleRequest rule,
        WorkflowTicketRuleTaskContext taskContext, string rulePath)
    {
        AppendRuleReferenceList(elements, rule.SourceObjects, new(taskContext.Entities, ElemFieldType.source,
            WorkflowTicketEntityKind.AddressObject, taskContext.FlowReferences, $"{rulePath}.sourceObjects", taskContext.Validation));
        AppendRuleReferenceList(elements, rule.SourceGroups, new(taskContext.Entities, ElemFieldType.source,
            WorkflowTicketEntityKind.AddressGroup, taskContext.FlowReferences, $"{rulePath}.sourceGroups", taskContext.Validation));
        AppendRuleReferenceList(elements, rule.DestinationObjects, new(taskContext.Entities, ElemFieldType.destination,
            WorkflowTicketEntityKind.AddressObject, taskContext.FlowReferences, $"{rulePath}.destinationObjects", taskContext.Validation));
        AppendRuleReferenceList(elements, rule.DestinationGroups, new(taskContext.Entities, ElemFieldType.destination,
            WorkflowTicketEntityKind.AddressGroup, taskContext.FlowReferences, $"{rulePath}.destinationGroups", taskContext.Validation));
        AppendRuleReferenceList(elements, rule.ServiceObjects, new(taskContext.Entities, ElemFieldType.service,
            WorkflowTicketEntityKind.ServiceObject, taskContext.FlowReferences, $"{rulePath}.serviceObjects", taskContext.Validation));
        AppendRuleReferenceList(elements, rule.ServiceGroups, new(taskContext.Entities, ElemFieldType.service,
            WorkflowTicketEntityKind.ServiceGroup, taskContext.FlowReferences, $"{rulePath}.serviceGroups", taskContext.Validation));
    }

    private static void AppendRuleReferenceList(List<WfReqElement> elements, IEnumerable<long> references,
        WorkflowTicketReferenceContext referenceContext)
    {
        WorkflowTicketElementValidation.AppendReferencedElements(elements, references,
            new(referenceContext.Validation.InvalidEntityIds, referenceContext.Validation.ValidationErrors,
                referenceContext.Path,
                reference => BuildReferencedElement(reference, referenceContext.Entities, referenceContext.Field,
                    referenceContext.ExpectedKind, referenceContext.FlowReferences)));
    }

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
                    RequestAction = entity.LeafRequestAction,
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
                    RequestAction = entity.LeafRequestAction,
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
                RequestAction = entity.LeafRequestAction,
                Name = entity.DisplayName,
                IpString = entity.IpStart,
                IpEnd = entity.IpEnd
            },
            WorkflowTicketEntityKind.ServiceObject => new WfReqElement
            {
                Field = field.ToString(),
                RequestAction = entity.LeafRequestAction,
                Name = entity.DisplayName,
                Port = entity.PortStart,
                PortEnd = entity.PortEnd,
                ProtoId = entity.ProtocolId
            },
            _ => throw new ArgumentException($"Member id {memberId} cannot be used in a group.")
        };
    }

    private static string BuildGroupAdditionalInfo(CreateTicketRequest request, string groupName, long groupId)
    {
        Dictionary<string, string> additionalInfo = BuildRequestContactInfo(request.RuleContactName, request.RuleContactId, request.RequestorName, request.RequestorId);
        additionalInfo[AdditionalInfoKeys.GrpName] = groupName;
        additionalInfo[AdditionalInfoKeys.GroupId] = groupId.ToString(CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(additionalInfo);
    }

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

    private static void AddEntity(Dictionary<long, WorkflowTicketEntity> entities, long id, WorkflowTicketEntity entity)
    {
        if (entities.TryAdd(id, entity))
        {
            return;
        }

        throw new ArgumentException($"Duplicate request object id {id}.");
    }

    private static void AddValidationError(List<RequestValidationError> validationErrors, string path, ArgumentException exception)
    {
        string errorPath = exception.Message is "not implemented yet" || exception.Message.StartsWith("Unknown predicate", StringComparison.Ordinal)
            ? $"{path}.predicate"
            : path;
        validationErrors.Add(new RequestValidationError { Path = errorPath, Message = exception.Message });
    }

    private static WorkflowTicketEntity GetEntity(Dictionary<long, WorkflowTicketEntity> entities, long id)
    {
        if (entities.TryGetValue(id, out WorkflowTicketEntity? entity))
        {
            return entity;
        }

        throw new ArgumentException($"Unknown request object id {id}.");
    }

    private static WfApproval BuildApproval(int stateId)
    {
        return new WfApproval
        {
            StateId = stateId,
            InitialApproval = true
        };
    }

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

    private static long ParseLocalEntityId(long id, string entityType)
    {
        if (id < 0)
        {
            return id;
        }

        throw new ArgumentException($"The {entityType} id '{id}' must be a negative, non-zero integer because positive ids reference existing Flow objects.");
    }
}
