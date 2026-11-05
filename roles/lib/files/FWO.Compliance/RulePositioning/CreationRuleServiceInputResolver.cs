using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using CreationRuleServiceResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleServiceInput,
    FWO.Compliance.RulePositioning.CreationRuleServiceResolutionFailureCategory>;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule services from ordered ticket and Flow state.</summary>
public sealed class CreationRuleServiceInputResolver(IFlowGroupResolver flowGroupResolver)
    : ICreationRuleServiceInputResolver
{
    private static readonly IReadOnlyCollection<string> kEmptyResolutionPath = Array.Empty<string>();
    private readonly IFlowGroupResolver flowGroupResolver = flowGroupResolver ??
        throw new ArgumentNullException(nameof(flowGroupResolver));

    /// <inheritdoc />
    public async Task<CreationRuleServiceResolutionResult> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAccessTask(accessTask);
            if (!TicketRequestStateProjector.TryProject(accessTask, ticketTasks, out IReadOnlyList<WfReqTask> orderedTasks))
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
            }
            List<WfReqElement> requested = ActiveServices(accessTask);
            if (requested.Count == 0)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
            }

            CatalogIndex catalog = await LoadCatalogAsync(requested, orderedTasks, cancellationToken);
            ResolutionContext context = new(accessTask, orderedTasks, catalog, cancellationToken);
            List<PositioningService> resolved = ResolveElements(requested, context, kEmptyResolutionPath);
            List<PositioningService> distinct = resolved
                .DistinctBy(service => (service.ProtocolId, service.DestinationPortStart, service.DestinationPortEnd))
                .ToList();
            return CreationRuleServiceResolutionResult.Success(new CreationRuleServiceInput(distinct));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ServiceResolutionException exception)
        {
            return CreationRuleServiceResolutionResult.Failure(exception.Category);
        }
        catch (Exception)
        {
            return CreationRuleServiceResolutionResult.Failure(
                CreationRuleServiceResolutionFailureCategory.UnexpectedFailure);
        }
    }

    /// <summary>Rejects task shapes outside new access-rule creation.</summary>
    private static void ValidateAccessTask(WfReqTask task)
    {
        if (task == null || task.Elements == null)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
        }
        if (task.TaskType != WfTaskType.access.ToString() ||
            task.RequestAction != RequestAction.create.ToString())
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.UnsupportedTask);
        }
    }

    /// <summary>Returns active service elements and rejects unknown member actions.</summary>
    private static List<WfReqElement> ActiveServices(WfReqTask task)
    {
        List<WfReqElement> result = [];
        foreach (WfReqElement element in task.Elements.Where(element => element.Field == ElemFieldType.service.ToString()))
        {
            if (!Enum.TryParse(element.RequestAction, false, out RequestAction action))
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
            }
            if (action != RequestAction.delete)
            {
                result.Add(element);
            }
        }
        return result;
    }

    /// <summary>Loads catalog service groups not fully defined by ticket state, plus modify baselines.</summary>
    private async Task<CatalogIndex> LoadCatalogAsync(
        IEnumerable<WfReqElement> accessElements,
        IReadOnlyList<WfReqTask> tasks,
        CancellationToken cancellationToken)
    {
        List<ServiceIdentity> ticketIdentities = tasks.Where(IsServiceGroupTask)
            .Select(GroupIdentityForTask).ToList();
        List<ServiceIdentity> references = accessElements.Concat(tasks.Where(IsServiceGroupTask)
            .SelectMany(task => task.Elements.Where(IsServiceElement)))
            .Where(IsExplicitGroupReference).Select(GroupIdentityForElement).ToList();
        references.AddRange(tasks.Where(task => task.TaskType == WfTaskType.group_modify.ToString() &&
            IsServiceGroupTask(task)).Select(GroupIdentityForTask));
        List<ServiceIdentity> requested = references.Where(reference =>
            !ticketIdentities.Any(ticket => Matches(reference, ticket)) ||
            tasks.Any(task => task.TaskType == WfTaskType.group_modify.ToString() &&
                IsServiceGroupTask(task) && Matches(reference, GroupIdentityForTask(task)))).ToList();
        List<long> ids = requested.Where(reference => reference.FlowId is > 0)
            .Select(reference => reference.FlowId!.Value).Distinct().ToList();
        List<string> names = requested.Where(reference => reference.FlowId is not > 0 && reference.Name.Length > 0)
            .Select(reference => reference.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0 && names.Count == 0)
        {
            return new CatalogIndex([]);
        }

        try
        {
            List<FlowServiceGroupResolution> groups = [];
            foreach (List<long> chunk in ids.Chunk(FlowGroupResolutionParameters.MaxSelectors).Select(values => values.ToList()))
            {
                groups.AddRange(await LoadCatalogChunkAsync(
                    new FlowGroupResolutionParameters { ServiceGroupIds = chunk }, cancellationToken));
            }
            foreach (List<string> chunk in names.Chunk(FlowGroupResolutionParameters.MaxSelectors).Select(values => values.ToList()))
            {
                groups.AddRange(await LoadCatalogChunkAsync(
                    new FlowGroupResolutionParameters { ServiceGroupNames = chunk }, cancellationToken));
            }
            return new CatalogIndex(groups);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ServiceResolutionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupQueryFailed);
        }
    }

    /// <summary>Loads and validates one bounded catalog selector chunk.</summary>
    private async Task<List<FlowServiceGroupResolution>> LoadCatalogChunkAsync(
        FlowGroupResolutionParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FlowGroupResolutionResult? result = await flowGroupResolver.ResolveFlowGroupMembersAsync(
            parameters, cancellationToken);
        if (result?.ServiceGroups == null ||
            result.ServiceGroups.Any(group => group == null || group.Members == null))
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupQueryFailed);
        }
        return result.ServiceGroups;
    }

    /// <summary>Recursively resolves direct service leaves and service-group references.</summary>
    private static List<PositioningService> ResolveElements(
        IEnumerable<WfReqElement> elements,
        ResolutionContext context,
        IReadOnlyCollection<string> visited)
    {
        List<PositioningService> services = [];
        foreach (WfReqElement element in elements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (HasTechnicalService(element))
            {
                services.Add(ResolveObject(element, context));
            }
            else if (IsExplicitGroupReference(element))
            {
                services.AddRange(ResolveGroup(GroupIdentityForElement(element), context, visited));
            }
            else if (HasObjectIdentity(element))
            {
                services.Add(ResolveObject(element, context));
            }
            else
            {
                services.Add(ParseService(element));
            }
        }
        return services;
    }

    /// <summary>Applies a matching ticket service-object task before parsing one leaf.</summary>
    private static PositioningService ResolveObject(WfReqElement requested, ResolutionContext context)
    {
        ServiceIdentity identity = ObjectIdentityForElement(requested);
        List<WfReqTask> matching = identity.IsUsable
            ? context.Tasks.Where(IsServiceObjectTask)
                .Where(task => task.Elements.Any(element => Matches(identity, ObjectIdentityForElement(element))))
                .ToList()
            : [];
        if (matching.Any(task => !TicketRequestStateProjector.IsBefore(task, context.AccessTask)))
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.ProtocolOrderInvalid);
        }
        if (matching.Count > 0)
        {
            WfReqElement? effective = matching.Select(WfObjectTaskHelper.GetRequestedElement)
                .LastOrDefault(element => element != null);
            if (effective == null)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.ServiceUnresolved);
            }
            requested = effective;
        }
        return ParseService(requested);
    }

    /// <summary>Resolves ticket group state first and otherwise one flattened catalog service group.</summary>
    private static List<PositioningService> ResolveGroup(
        ServiceIdentity identity,
        ResolutionContext context,
        IReadOnlyCollection<string> visited)
    {
        if (!identity.IsUsable)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.ServiceUnresolved);
        }
        if (visited.Count >= TicketRequestStateProjector.kMaximumGroupDepth || visited.Contains(identity.Key))
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
        }
        List<WfReqTask> tasks = context.Tasks.Where(IsServiceGroupTask)
            .Where(task => Matches(identity, GroupIdentityForTask(task))).ToList();
        if (tasks.Any(task => !TicketRequestStateProjector.IsBefore(task, context.AccessTask)))
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.ProtocolOrderInvalid);
        }
        IReadOnlyCollection<string> path = [.. visited, identity.Key];
        return tasks.Count > 0
            ? ResolveTicketGroup(identity, tasks, context, path)
            : ResolveCatalogMembers(context.Catalog.Resolve(identity), context.CancellationToken);
    }

    /// <summary>Folds ordered create, modify, and delete tasks into one effective service-group member set.</summary>
    private static List<PositioningService> ResolveTicketGroup(
        ServiceIdentity identity,
        IReadOnlyList<WfReqTask> tasks,
        ResolutionContext context,
        IReadOnlyCollection<string> path)
    {
        List<WfReqElement> members = [];
        bool initialized = false;
        bool deleted = false;
        foreach (WfReqTask task in tasks.OrderBy(task => task.TaskNumber).ThenBy(task => task.Id))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (task.TaskType == WfTaskType.group_delete.ToString())
            {
                members.Clear();
                initialized = true;
                deleted = true;
                continue;
            }
            if (!initialized && task.TaskType == WfTaskType.group_modify.ToString())
            {
                members.AddRange(CatalogMembersAsElements(context.Catalog.Resolve(identity)));
            }
            else if (!initialized && task.TaskType != WfTaskType.group_create.ToString())
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
            }
            initialized = true;
            deleted = false;
            ApplyMemberActions(members, task.Elements.Where(IsServiceElement));
        }
        if (!initialized || deleted || members.Count == 0)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
        }
        return ResolveElements(members, context, path);
    }

    /// <summary>Applies supported member changes with identity-based replacement and removal.</summary>
    private static void ApplyMemberActions(List<WfReqElement> members, IEnumerable<WfReqElement> changes)
    {
        foreach (WfReqElement change in changes)
        {
            if (!Enum.TryParse(change.RequestAction, false, out RequestAction action))
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
            }
            List<int> matches = Enumerable.Range(0, members.Count).Where(index =>
                Matches(ObjectIdentityForElement(change), ObjectIdentityForElement(members[index])) ||
                Matches(GroupIdentityForElement(change), GroupIdentityForElement(members[index]))).ToList();
            if (matches.Count > 1)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.GroupAmbiguous);
            }
            ApplyMemberAction(members, change, action, matches);
        }
    }

    /// <summary>Applies one validated service-group member action.</summary>
    private static void ApplyMemberAction(
        List<WfReqElement> members,
        WfReqElement change,
        RequestAction action,
        List<int> matches)
    {
        switch (action)
        {
            case RequestAction.create:
            case RequestAction.addAfterCreation:
                members.Add(new WfReqElement(change));
                return;
            case RequestAction.unchanged:
                return;
            case RequestAction.modify:
                RequireSingleMemberMatch(matches);
                members[matches[0]] = new WfReqElement(change);
                return;
            case RequestAction.delete:
                RequireSingleMemberMatch(matches);
                members.RemoveAt(matches[0]);
                return;
            default:
                throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidInput);
        }
    }

    /// <summary>Rejects a member change without exactly one effective match.</summary>
    private static void RequireSingleMemberMatch(List<int> matches)
    {
        if (matches.Count != 1)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
        }
    }

    /// <summary>Normalizes the flattened leaves of one catalog service group.</summary>
    private static List<PositioningService> ResolveCatalogMembers(
        FlowServiceGroupResolution group,
        CancellationToken cancellationToken)
    {
        if (group.Members.Count == 0)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
        }
        List<PositioningService> services = [];
        foreach (FlowServiceMemberResolution member in group.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                services.Add(new PositioningService(member.ProtoId, member.PortStart, member.PortEnd));
            }
            catch (ArgumentException)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
            }
        }
        return services;
    }

    /// <summary>Projects flattened catalog members into the workflow member shape.</summary>
    private static IEnumerable<WfReqElement> CatalogMembersAsElements(FlowServiceGroupResolution group) =>
        group.Members.Select(member => new WfReqElement
        {
            FlowServiceObjectId = member.Id,
            Name = member.Name,
            ProtoId = member.ProtoId,
            Port = member.PortStart,
            PortEnd = member.PortEnd,
            Field = ElemFieldType.service.ToString(),
            RequestAction = RequestAction.unchanged.ToString()
        });

    /// <summary>Parses one workflow service element without widening malformed data.</summary>
    private static PositioningService ParseService(WfReqElement element)
    {
        if (!element.ProtoId.HasValue)
        {
            throw Failure(HasObjectIdentity(element)
                ? CreationRuleServiceResolutionFailureCategory.ServiceUnresolved
                : CreationRuleServiceResolutionFailureCategory.InvalidService);
        }
        try
        {
            return new PositioningService(element.ProtoId.Value, element.Port, element.PortEnd);
        }
        catch (ArgumentException)
        {
            throw Failure(CreationRuleServiceResolutionFailureCategory.InvalidService);
        }
    }

    /// <summary>Identifies service group tasks while excluding network-only groups.</summary>
    private static bool IsServiceGroupTask(WfReqTask task) =>
        (task.TaskType == WfTaskType.group_create.ToString() ||
         task.TaskType == WfTaskType.group_modify.ToString() ||
         task.TaskType == WfTaskType.group_delete.ToString()) &&
        WfObjectTaskHelper.OrdersServiceObject(task);

    /// <summary>Identifies service object tasks while excluding network objects.</summary>
    private static bool IsServiceObjectTask(WfReqTask task) =>
        WfObjectTaskHelper.IsObjectTask(task.TaskType) && WfObjectTaskHelper.OrdersServiceObject(task);

    /// <summary>Checks whether an element belongs to the service field.</summary>
    private static bool IsServiceElement(WfReqElement element) =>
        element.Field == ElemFieldType.service.ToString();

    /// <summary>Checks whether a service element carries a protocol definition.</summary>
    private static bool HasTechnicalService(WfReqElement element) => element.ProtoId.HasValue;

    /// <summary>Checks for workflow fields that explicitly identify a service-group reference.</summary>
    private static bool IsExplicitGroupReference(WfReqElement element) =>
        element.FlowServiceGroupId is > 0 || !string.IsNullOrWhiteSpace(element.GroupName);

    /// <summary>Checks whether a service-object reference has a usable stable identity.</summary>
    private static bool HasObjectIdentity(WfReqElement element) => ObjectIdentityForElement(element).IsUsable;

    /// <summary>Extracts the prioritized identity of a ticket service-group task.</summary>
    private static ServiceIdentity GroupIdentityForTask(WfReqTask task) => new(
        task.Elements.Select(element => element.FlowServiceGroupId).FirstOrDefault(id => id is > 0) ??
            task.GetAddInfoLongValue(AdditionalInfoKeys.SvcGrpId),
        task.ServiceGroupId,
        FirstName(task.GetAddInfoValue(AdditionalInfoKeys.GrpName), task.Title));

    /// <summary>Extracts the prioritized service-group identity of a workflow element.</summary>
    private static ServiceIdentity GroupIdentityForElement(WfReqElement element) => new(
        element.FlowServiceGroupId,
        element.ServiceId,
        FirstName(element.GroupName, element.Name));

    /// <summary>Extracts the prioritized service-object identity of a workflow element.</summary>
    private static ServiceIdentity ObjectIdentityForElement(WfReqElement element) => new(
        element.FlowServiceObjectId,
        element.ServiceId,
        element.Name?.Trim() ?? string.Empty);

    /// <summary>Returns the first normalized nonblank identity name.</summary>
    private static string FirstName(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first.Trim() : second?.Trim() ?? string.Empty;

    /// <summary>Correlates identities by Flow ID, normalized ID, then invariant name.</summary>
    private static bool Matches(ServiceIdentity left, ServiceIdentity right)
    {
        if (left.FlowId is > 0 || right.FlowId is > 0)
        {
            return left.FlowId is > 0 && left.FlowId == right.FlowId;
        }
        if (left.NormalizedId is > 0 || right.NormalizedId is > 0)
        {
            return left.NormalizedId is > 0 && left.NormalizedId == right.NormalizedId;
        }
        return left.Name.Length > 0 && right.Name.Length > 0 &&
            string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Creates an internal typed control-flow failure.</summary>
    private static ServiceResolutionException Failure(CreationRuleServiceResolutionFailureCategory category) => new(category);

    /// <summary>Immutable state shared while resolving nested ticket and catalog elements.</summary>
    private sealed record ResolutionContext(
        WfReqTask AccessTask,
        IReadOnlyList<WfReqTask> Tasks,
        CatalogIndex Catalog,
        CancellationToken CancellationToken);

    /// <summary>Prioritized identities used to match one service or service group.</summary>
    private sealed record ServiceIdentity(long? FlowId, long? NormalizedId, string Name)
    {
        /// <summary>Gets whether at least one stable identity is available.</summary>
        internal bool IsUsable => FlowId is > 0 || NormalizedId is > 0 || Name.Length > 0;

        /// <summary>Gets a stable recursion key based on the strongest identity.</summary>
        internal string Key
        {
            get
            {
                if (FlowId is > 0)
                {
                    return $"f:{FlowId}";
                }
                return NormalizedId is > 0
                    ? $"n:{NormalizedId}"
                    : $"s:{Name.ToUpperInvariant()}";
            }
        }
    }

    /// <summary>Lookup index for catalog service groups loaded by identifier or name.</summary>
    private sealed class CatalogIndex(IReadOnlyCollection<FlowServiceGroupResolution> groups)
    {
        /// <summary>Requires exactly one catalog group under the strongest available identity.</summary>
        internal FlowServiceGroupResolution Resolve(ServiceIdentity identity)
        {
            List<FlowServiceGroupResolution> matches = identity.FlowId is > 0
                ? [.. groups.Where(group => group.Id == identity.FlowId)]
                : [.. groups.Where(group => identity.Name.Length > 0 &&
                    string.Equals(group.Name.Trim(), identity.Name, StringComparison.OrdinalIgnoreCase))];
            if (matches.Count > 1)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.GroupAmbiguous);
            }
            if (matches.Count == 0)
            {
                throw Failure(CreationRuleServiceResolutionFailureCategory.GroupUnresolved);
            }
            return matches[0];
        }
    }
}

/// <summary>Signals one safely categorized service-input resolution failure.</summary>
public sealed class ServiceResolutionException(CreationRuleServiceResolutionFailureCategory category) : Exception
{
    /// <summary>Gets the safe stable failure category.</summary>
    public CreationRuleServiceResolutionFailureCategory Category { get; } = category;
}
