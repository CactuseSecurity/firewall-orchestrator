using System.Runtime.CompilerServices;
using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Services.RulePositioning;
using NetTools;
using CreationRuleNetworkResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleNetworkInput,
    FWO.Compliance.RulePositioning.CreationRuleNetworkResolutionFailureCategory>;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule addresses from ordered ticket and Flow state.</summary>
public sealed class CreationRuleNetworkInputResolver(IFlowGroupResolver flowGroupResolver)
    : ICreationRuleNetworkInputResolver
{
    private static readonly IReadOnlyCollection<string> kEmptyResolutionPath =
        new List<string>().AsReadOnly();
    private readonly IFlowGroupResolver flowGroupResolver = flowGroupResolver ??
        throw new ArgumentNullException(nameof(flowGroupResolver));

    /// <inheritdoc />
    public async Task<CreationRuleNetworkResolutionResult> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken)
    {
        string currentOperation = "network.validate";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAccessTask(accessTask);
            currentOperation = "network.ticket_projection";
            IReadOnlyList<WfReqTask> orderedTasks = TicketNetworkStateProjector.Project(
                accessTask,
                ticketTasks);
            currentOperation = "network.active_sides";
            List<WfReqElement> sources = ActiveSide(accessTask, ElemFieldType.source);
            List<WfReqElement> destinations = ActiveSide(accessTask, ElemFieldType.destination);
            RulePositioningDiagnosticLog.WriteInfo(
                "network.prepare",
                "summary",
                Facts(
                    ("task_id", accessTask.Id),
                    ("task_type", accessTask.TaskType),
                    ("request_action", accessTask.RequestAction),
                    ("ticket_task_count", orderedTasks.Count),
                    ("minimum_task_order", orderedTasks.Count == 0 ? 0 : orderedTasks.Min(Task => Task.TaskNumber)),
                    ("maximum_task_order", orderedTasks.Count == 0 ? 0 : orderedTasks.Max(Task => Task.TaskNumber)),
                    ("total_source_count", accessTask.Elements.Count(Element => Element.Field == ElemFieldType.source.ToString())),
                    ("active_source_count", sources.Count),
                    ("total_destination_count", accessTask.Elements.Count(Element => Element.Field == ElemFieldType.destination.ToString())),
                    ("active_destination_count", destinations.Count),
                    ("direct_address_count", sources.Concat(destinations).Count(HasAddress)),
                    ("group_reference_count", sources.Concat(destinations).Count(Element => !HasAddress(Element)))));
            if (sources.Count == 0 || destinations.Count == 0)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }

            currentOperation = "network.catalog";
            CatalogIndex catalog = await LoadCatalogAsync(
                sources.Concat(destinations), orderedTasks, cancellationToken);
            ResolutionContext context = new(accessTask, orderedTasks, catalog, cancellationToken);
            currentOperation = "network.resolve_sources";
            List<IPAddressRange> sourceRanges = ResolveElements(sources, context, kEmptyResolutionPath);
            currentOperation = "network.resolve_destinations";
            List<IPAddressRange> destinationRanges = ResolveElements(destinations, context, kEmptyResolutionPath);
            IReadOnlyList<IPAddressRange> normalizedSources = PositioningAddressRangeNormalizer.Distinct(sourceRanges);
            IReadOnlyList<IPAddressRange> normalizedDestinations = PositioningAddressRangeNormalizer.Distinct(destinationRanges);
            RulePositioningDiagnosticLog.WriteInfo(
                "network.resolve",
                "success",
                Facts(
                    ("source_range_count", normalizedSources.Count),
                    ("destination_range_count", normalizedDestinations.Count),
                    ("source_address_families", AddressFamilies(normalizedSources)),
                    ("destination_address_families", AddressFamilies(normalizedDestinations))));
            return CreationRuleNetworkResolutionResult.Success(new CreationRuleNetworkInput(
                normalizedSources,
                normalizedDestinations));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            RulePositioningDiagnosticLog.WriteException(currentOperation, exception);
            throw;
        }
        catch (NetworkResolutionException exception)
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "network.resolve",
                Facts(
                    ("operation", currentOperation),
                    ("task_id", accessTask?.Id ?? 0),
                    ("failure_category", exception.Category.ToString())));
            return CreationRuleNetworkResolutionResult.Failure(exception.Category);
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException(
                "network.resolve",
                exception,
                Facts(("operation", currentOperation), ("task_id", accessTask?.Id ?? 0)));
            return CreationRuleNetworkResolutionResult.Failure(
                CreationRuleNetworkResolutionFailureCategory.UnexpectedFailure);
        }
    }

    /// <summary>Rejects task shapes outside new access-rule creation.</summary>
    private static void ValidateAccessTask(WfReqTask task)
    {
        if (task == null || task.Elements == null)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
        if (!string.Equals(task.TaskType, WfTaskType.access.ToString(), StringComparison.Ordinal) ||
            !string.Equals(task.RequestAction, RequestAction.create.ToString(), StringComparison.Ordinal))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.UnsupportedTask);
        }
    }

    /// <summary>Returns active elements for one side and rejects unknown actions.</summary>
    private static List<WfReqElement> ActiveSide(WfReqTask task, ElemFieldType field)
    {
        List<WfReqElement> result = [];
        foreach (WfReqElement element in task.Elements.Where(element => element.Field == field.ToString()))
        {
            if (!Enum.TryParse(element.RequestAction, false, out RequestAction action))
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }
            if (action != RequestAction.delete)
            {
                result.Add(element);
            }
        }
        return result;
    }

    /// <summary>Loads only catalog groups not fully defined by ticket state, plus modify baselines.</summary>
    private async Task<CatalogIndex> LoadCatalogAsync(
        IEnumerable<WfReqElement> accessElements,
        IReadOnlyList<WfReqTask> tasks,
        CancellationToken cancellationToken)
    {
        List<GroupIdentity> ticketIdentities = tasks.Where(IsNetworkGroupTask).Select(GroupIdentityForTask).ToList();
        List<GroupIdentity> references = accessElements.Concat(tasks.Where(IsNetworkGroupTask)
            .SelectMany(task => task.Elements.Where(element => element.Field == ElemFieldType.source.ToString())))
            .Where(element => !HasAddress(element)).Select(GroupIdentityForElement).ToList();
        references.AddRange(tasks.Where(task => task.TaskType == WfTaskType.group_modify.ToString())
            .Select(GroupIdentityForTask));
        List<GroupIdentity> requested = references.Where(reference =>
            !ticketIdentities.Any(ticket => Matches(reference, ticket)) ||
            tasks.Any(task => task.TaskType == WfTaskType.group_modify.ToString() &&
                Matches(reference, GroupIdentityForTask(task)))).ToList();
        List<long> ids = requested.Where(reference => reference.FlowId is > 0)
            .Select(reference => reference.FlowId!.Value).Distinct().ToList();
        List<string> names = requested.Where(reference => reference.FlowId is not > 0 && reference.Name.Length > 0)
            .Select(reference => reference.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        RulePositioningDiagnosticLog.WriteInfo(
            "network.catalog.selectors",
            "summary",
            Facts(
                ("id_selector_count", ids.Count),
                ("name_selector_count", names.Count),
                ("id_chunk_count", (ids.Count + FlowGroupResolutionParameters.MaxSelectors - 1) /
                    FlowGroupResolutionParameters.MaxSelectors),
                ("name_chunk_count", (names.Count + FlowGroupResolutionParameters.MaxSelectors - 1) /
                    FlowGroupResolutionParameters.MaxSelectors)));
        if (ids.Count == 0 && names.Count == 0)
        {
            return new CatalogIndex(new List<FlowNetworkGroupResolution>());
        }
        try
        {
            List<FlowNetworkGroupResolution> groups = [];
            foreach (List<long> chunk in ids.Chunk(FlowGroupResolutionParameters.MaxSelectors)
                .Select(values => values.ToList()))
            {
                groups.AddRange(await LoadCatalogChunkAsync(
                    new FlowGroupResolutionParameters { NetworkGroupIds = chunk }, cancellationToken));
            }
            foreach (List<string> chunk in names.Chunk(FlowGroupResolutionParameters.MaxSelectors)
                .Select(values => values.ToList()))
            {
                groups.AddRange(await LoadCatalogChunkAsync(
                    new FlowGroupResolutionParameters { NetworkGroupNames = chunk }, cancellationToken));
            }
            RulePositioningDiagnosticLog.WriteInfo(
                "network.catalog.result",
                "success",
                Facts(
                    ("returned_group_count", groups.Count),
                    ("returned_member_count", groups.Sum(Group => Group.Members?.Count ?? 0))));
            return new CatalogIndex(groups);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NetworkResolutionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed);
        }
    }

    /// <summary>Loads and validates one bounded catalog selector chunk.</summary>
    private async Task<List<FlowNetworkGroupResolution>> LoadCatalogChunkAsync(
        FlowGroupResolutionParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FlowGroupResolutionResult? result = await flowGroupResolver.ResolveFlowGroupMembersAsync(
            parameters,
            cancellationToken);
        if (result?.NetworkGroups == null ||
            result.NetworkGroups.Any(group => group == null || group.Members == null))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed);
        }
        return result.NetworkGroups;
    }

    /// <summary>Recursively resolves direct leaves and group references.</summary>
    private static List<IPAddressRange> ResolveElements(
        IEnumerable<WfReqElement> elements,
        ResolutionContext context,
        IReadOnlyCollection<string> visited)
    {
        List<IPAddressRange> ranges = [];
        foreach (WfReqElement element in elements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (HasAddress(element))
            {
                ranges.Add(ResolveObject(element, context));
            }
            else
            {
                ranges.AddRange(ResolveGroup(GroupIdentityForElement(element), context, visited));
            }
        }
        return ranges;
    }

    /// <summary>Applies a matching ticket object task before parsing a direct leaf.</summary>
    private static IPAddressRange ResolveObject(WfReqElement requested, ResolutionContext context)
    {
        GroupIdentity identity = ObjectIdentityForElement(requested);
        List<WfReqTask> matching = context.Tasks.Where(IsNetworkObjectTask)
            .Where(task => task.Elements.Any(element => Matches(identity, ObjectIdentityForElement(element))))
            .ToList();
        if (matching.Any(task => !TicketNetworkStateProjector.IsBefore(task, context.AccessTask)))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid,
                IdentityFacts(identity, matching.Count));
        }
        if (matching.Count > 0)
        {
            WfReqElement? effective = matching.Select(WfObjectTaskHelper.GetRequestedElement).LastOrDefault(element => element != null);
            if (effective == null)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved,
                    IdentityFacts(identity, matching.Count));
            }
            requested = effective;
        }
        return PositioningAddressRangeNormalizer.Normalize(requested);
    }

    /// <summary>Resolves ticket state first and falls back to one flattened catalog group.</summary>
    private static List<IPAddressRange> ResolveGroup(
        GroupIdentity identity,
        ResolutionContext context,
        IReadOnlyCollection<string> visited)
    {
        if (!identity.IsUsable)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved, IdentityFacts(identity, 0));
        }
        string visitKey = identity.Key;
        if (visited.Count >= TicketNetworkStateProjector.kMaximumGroupDepth || visited.Contains(visitKey))
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "network.group.recursion",
                Facts(
                    ("depth", visited.Count),
                    ("maximum_depth", TicketNetworkStateProjector.kMaximumGroupDepth),
                    ("cycle", visited.Contains(visitKey)),
                    ("group_key", RulePositioningDiagnosticLog.Fingerprint(visitKey))));
            Dictionary<string, object?> facts = IdentityFacts(identity);
            facts["depth"] = visited.Count;
            facts["cycle"] = visited.Contains(visitKey);
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved, facts);
        }
        List<WfReqTask> ticketTasks = context.Tasks.Where(IsNetworkGroupTask)
            .Where(task => Matches(identity, GroupIdentityForTask(task))).ToList();
        if (ticketTasks.Any(task => !TicketNetworkStateProjector.IsBefore(task, context.AccessTask)))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid,
                IdentityFacts(identity, ticketTasks.Count));
        }
        IReadOnlyCollection<string> path = [.. visited, visitKey];
        if (ticketTasks.Count > 0)
        {
            return ResolveTicketGroup(identity, ticketTasks, context, path);
        }
        FlowNetworkGroupResolution catalogGroup = context.Catalog.Resolve(identity);
        return ResolveCatalogMembers(catalogGroup, context.CancellationToken);
    }

    /// <summary>Folds ordered create/modify/delete group tasks to one final member set.</summary>
    private static List<IPAddressRange> ResolveTicketGroup(
        GroupIdentity identity,
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
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }
            initialized = true;
            deleted = false;
            ApplyMemberActions(members, task.Elements.Where(element =>
                element.Field == ElemFieldType.source.ToString()));
        }
        if (!initialized || deleted || members.Count == 0)
        {
            Dictionary<string, object?> facts = IdentityFacts(identity, tasks.Count);
            facts["member_count"] = members.Count;
            facts["deleted"] = deleted;
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved, facts);
        }
        return ResolveElements(members, context, path);
    }

    /// <summary>Applies supported group member actions with identity-based replacement/removal.</summary>
    private static void ApplyMemberActions(List<WfReqElement> members, IEnumerable<WfReqElement> changes)
    {
        foreach (WfReqElement change in changes)
        {
            if (!Enum.TryParse(change.RequestAction, false, out RequestAction action))
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }
            List<int> matches = Enumerable.Range(0, members.Count).Where(index =>
                Matches(ObjectIdentityForElement(change), ObjectIdentityForElement(members[index])) ||
                Matches(GroupIdentityForElement(change), GroupIdentityForElement(members[index]))).ToList();
            if (matches.Count > 1)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous,
                    IdentityFacts(IdentityForElement(change), matches.Count));
            }
            ApplyMemberAction(members, change, action, matches);
        }
    }

    /// <summary>Applies one validated group member action.</summary>
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
                members.Add(Clone(change));
                return;
            case RequestAction.unchanged:
                return;
            case RequestAction.modify:
                RequireSingleMemberMatch(change, matches);
                members[matches[0]] = Clone(change);
                return;
            case RequestAction.delete:
                RequireSingleMemberMatch(change, matches);
                members.RemoveAt(matches[0]);
                return;
            default:
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
    }

    /// <summary>Rejects a group member change without exactly one existing match.</summary>
    private static void RequireSingleMemberMatch(WfReqElement change, List<int> matches)
    {
        if (matches.Count != 1)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved,
                IdentityFacts(IdentityForElement(change), matches.Count));
        }
    }

    /// <summary>Normalizes the flattened address members of one catalog group.</summary>
    private static List<IPAddressRange> ResolveCatalogMembers(
        FlowNetworkGroupResolution group,
        CancellationToken cancellationToken)
    {
        if (group.Members.Count == 0)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved,
                IdentityFacts(new GroupIdentity(group.Id, null, group.Name ?? string.Empty), 0));
        }
        List<IPAddressRange> ranges = [];
        foreach (FlowNetworkMemberResolution member in group.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ranges.Add(PositioningAddressRangeNormalizer.Normalize(member.IpStart, member.IpEnd));
            }
            catch (NetworkResolutionException exception) when (
                exception.Category == CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved,
                    IdentityFacts(new GroupIdentity(group.Id, null, group.Name ?? string.Empty)));
            }
        }
        return ranges;
    }

    /// <summary>Projects flattened catalog members into the common ticket-member shape.</summary>
    private static IEnumerable<WfReqElement> CatalogMembersAsElements(FlowNetworkGroupResolution group) =>
        group.Members.Select(member => new WfReqElement
        {
            FlowNetworkObjectId = member.Id,
            Name = member.Name,
            IpString = member.IpStart,
            IpEnd = member.IpEnd,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.unchanged.ToString()
        });

    /// <summary>Identifies network group tasks while excluding service-only groups.</summary>
    private static bool IsNetworkGroupTask(WfReqTask task) =>
        (task.TaskType == WfTaskType.group_create.ToString() ||
         task.TaskType == WfTaskType.group_modify.ToString() ||
         task.TaskType == WfTaskType.group_delete.ToString()) &&
        !WfObjectTaskHelper.OrdersServiceObject(task);

    /// <summary>Identifies network object tasks while excluding service objects.</summary>
    private static bool IsNetworkObjectTask(WfReqTask task) =>
        WfObjectTaskHelper.IsObjectTask(task.TaskType) &&
        !WfObjectTaskHelper.OrdersServiceObject(task);

    /// <summary>Checks whether a workflow element carries concrete address data.</summary>
    private static bool HasAddress(WfReqElement element) =>
        !string.IsNullOrWhiteSpace(element.IpString) || element.Cidr?.Valid == true;

    /// <summary>Extracts the prioritized identity of a ticket group task.</summary>
    private static GroupIdentity GroupIdentityForTask(WfReqTask task) => new(
        task.GetAddInfoLongValue(AdditionalInfoKeys.GroupId) ??
            task.Elements.Select(element => element.FlowNetworkGroupId).FirstOrDefault(id => id is > 0),
        task.NetworkGroupId,
        FirstName(task.GetAddInfoValue(AdditionalInfoKeys.GrpName), task.Title));

    /// <summary>Extracts the prioritized group identity of a workflow element.</summary>
    private static GroupIdentity GroupIdentityForElement(WfReqElement element) => new(
        element.FlowNetworkGroupId,
        element.NetworkId,
        FirstName(element.GroupName, element.Name));

    /// <summary>Extracts the prioritized object identity of a workflow element.</summary>
    private static GroupIdentity ObjectIdentityForElement(WfReqElement element) => new(
        element.FlowNetworkObjectId,
        element.NetworkId,
        element.Name?.Trim() ?? string.Empty);

    /// <summary>Chooses the same prioritized identity family used for member matching.</summary>
    private static GroupIdentity IdentityForElement(WfReqElement element) =>
        element.FlowNetworkGroupId is > 0 || !string.IsNullOrWhiteSpace(element.GroupName)
            ? GroupIdentityForElement(element)
            : ObjectIdentityForElement(element);

    /// <summary>Returns the first normalized nonblank identity name.</summary>
    private static string FirstName(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first.Trim() : second?.Trim() ?? string.Empty;

    /// <summary>Correlates identities by Flow ID, normalized ID, then invariant name.</summary>
    private static bool Matches(GroupIdentity left, GroupIdentity right)
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

    /// <summary>Copies a workflow element before retaining it in projected state.</summary>
    private static WfReqElement Clone(WfReqElement element) => new(element);

    /// <summary>Creates an internal typed control-flow failure.</summary>
    private static NetworkResolutionException Failure(
        CreationRuleNetworkResolutionFailureCategory category,
        IReadOnlyDictionary<string, object?>? context = null,
        [CallerMemberName] string operation = "")
    {
        Dictionary<string, object?> facts = Facts(
            ("operation", operation),
            ("failure_category", category.ToString()));
        if (context != null)
        {
            foreach ((string key, object? value) in context)
            {
                facts.TryAdd(key, value);
            }
        }
        RulePositioningDiagnosticLog.WriteFailure(
            "network.failure",
            facts);
        return new NetworkResolutionException(category);
    }

    /// <summary>Builds a safe identity summary without retaining a fallback name.</summary>
    private static Dictionary<string, object?> IdentityFacts(GroupIdentity identity, int? matchCount = null)
    {
        string source = identity.FlowId is > 0
            ? "flow_id"
            : identity.NormalizedId is > 0 ? "normalized_id" : identity.Name.Length > 0 ? "name" : "none";
        Dictionary<string, object?> facts = Facts(
            ("identity_source", source),
            ("flow_id", identity.FlowId is > 0 ? identity.FlowId : null),
            ("normalized_id", identity.NormalizedId is > 0 ? identity.NormalizedId : null),
            ("fallback_name", source == "name" ? RulePositioningDiagnosticLog.Fingerprint(identity.Name) : null));
        if (matchCount.HasValue)
        {
            facts["match_count"] = matchCount.Value;
        }
        return facts;
    }

    /// <summary>Returns the distinct address-family names represented by safe normalized ranges.</summary>
    private static List<string> AddressFamilies(IEnumerable<IPAddressRange> ranges) =>
        ranges.Select(Range => Range.Begin.AddressFamily.ToString()).Distinct().Order().ToList();

    /// <summary>Builds one bounded network diagnostic fact map.</summary>
    private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
        values.ToDictionary(Value => Value.Key, Value => Value.Value);

    /// <summary>Immutable state shared while resolving nested ticket and catalog elements.</summary>
    private sealed record ResolutionContext(
        WfReqTask AccessTask,
        IReadOnlyList<WfReqTask> Tasks,
        CatalogIndex Catalog,
        CancellationToken CancellationToken);

    /// <summary>Prioritized identities used to match one network group.</summary>
    private sealed record GroupIdentity(long? FlowId, long? NormalizedId, string Name)
    {
        /// <summary>Gets whether at least one stable group identity is available.</summary>
        internal bool IsUsable => FlowId is > 0 || NormalizedId is > 0 || Name.Length > 0;
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

    /// <summary>Lookup index for catalog groups loaded by identifier or name.</summary>
    private sealed class CatalogIndex(IReadOnlyCollection<FlowNetworkGroupResolution> groups)
    {
        /// <summary>Requires exactly one catalog group under the highest available identity.</summary>
        internal FlowNetworkGroupResolution Resolve(GroupIdentity identity)
        {
            List<FlowNetworkGroupResolution> matches = identity.FlowId is > 0
                ? [.. groups.Where(group => group.Id == identity.FlowId)]
                : [.. groups.Where(group => identity.Name.Length > 0 &&
                    string.Equals(group.Name.Trim(), identity.Name, StringComparison.OrdinalIgnoreCase))];
            if (matches.Count > 1)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous,
                    IdentityFacts(identity, matches.Count));
            }
            if (matches.Count == 0)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved,
                    IdentityFacts(identity, matches.Count));
            }
            return matches[0];
        }
    }
}
