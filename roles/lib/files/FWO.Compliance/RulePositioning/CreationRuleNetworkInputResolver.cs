using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NetTools;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule addresses from ordered ticket and Flow state.</summary>
public sealed class CreationRuleNetworkInputResolver(IFlowGroupResolver flowGroupResolver)
    : ICreationRuleNetworkInputResolver
{
    private readonly IFlowGroupResolver flowGroupResolver = flowGroupResolver ??
        throw new ArgumentNullException(nameof(flowGroupResolver));
    private readonly TicketNetworkStateProjector projector = new();

    /// <inheritdoc />
    public async Task<CreationRuleNetworkResolutionResult> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAccessTask(accessTask);
            IReadOnlyList<WfReqTask> orderedTasks = projector.Project(accessTask, ticketTasks);
            List<WfReqElement> sources = ActiveSide(accessTask, ElemFieldType.source);
            List<WfReqElement> destinations = ActiveSide(accessTask, ElemFieldType.destination);
            if (sources.Count == 0 || destinations.Count == 0)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }

            CatalogIndex catalog = await LoadCatalogAsync(
                sources.Concat(destinations), orderedTasks, cancellationToken);
            ResolutionContext context = new(accessTask, orderedTasks, catalog, cancellationToken);
            List<IPAddressRange> sourceRanges = ResolveElements(sources, context, []);
            List<IPAddressRange> destinationRanges = ResolveElements(destinations, context, []);
            return new CreationRuleNetworkResolutionSuccess(new CreationRuleNetworkInput(
                PositioningAddressRangeNormalizer.Distinct(sourceRanges),
                PositioningAddressRangeNormalizer.Distinct(destinationRanges)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NetworkResolutionException exception)
        {
            return new CreationRuleNetworkResolutionFailure(exception.Category);
        }
        catch (Exception)
        {
            return new CreationRuleNetworkResolutionFailure(
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
        if (ids.Count == 0 && names.Count == 0)
        {
            return new CatalogIndex([]);
        }
        if (ids.Count + names.Count > FlowGroupResolutionParameters.MaxSelectors)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }

        try
        {
            FlowGroupResolutionResult? result = await flowGroupResolver.ResolveFlowGroupMembersAsync(new()
            {
                NetworkGroupIds = ids,
                NetworkGroupNames = names
            }, cancellationToken);
            if (result?.NetworkGroups == null || result.NetworkGroups.Any(group => group == null || group.Members == null))
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed);
            }
            return new CatalogIndex(result.NetworkGroups);
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
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid);
        }
        if (matching.Count > 0)
        {
            WfReqElement? effective = matching.Select(WfObjectTaskHelper.GetRequestedElement).LastOrDefault(element => element != null);
            if (effective == null)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
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
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
        }
        string visitKey = identity.Key;
        if (visited.Count >= TicketNetworkStateProjector.kMaximumGroupDepth || visited.Contains(visitKey))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
        List<WfReqTask> ticketTasks = context.Tasks.Where(IsNetworkGroupTask)
            .Where(task => Matches(identity, GroupIdentityForTask(task))).ToList();
        if (ticketTasks.Any(task => !TicketNetworkStateProjector.IsBefore(task, context.AccessTask)))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid);
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
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
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
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous);
            }
            switch (action)
            {
                case RequestAction.create:
                case RequestAction.addAfterCreation:
                    members.Add(Clone(change));
                    break;
                case RequestAction.unchanged:
                    break;
                case RequestAction.modify:
                    if (matches.Count != 1)
                    {
                        throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
                    }
                    members[matches[0]] = Clone(change);
                    break;
                case RequestAction.delete:
                    if (matches.Count != 1)
                    {
                        throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
                    }
                    members.RemoveAt(matches[0]);
                    break;
                default:
                    throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }
        }
    }

    private static List<IPAddressRange> ResolveCatalogMembers(
        FlowNetworkGroupResolution group,
        CancellationToken cancellationToken)
    {
        if (group.Members.Count == 0)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
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
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
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
    private static NetworkResolutionException Failure(CreationRuleNetworkResolutionFailureCategory category) => new(category);

    private sealed record ResolutionContext(
        WfReqTask AccessTask,
        IReadOnlyList<WfReqTask> Tasks,
        CatalogIndex Catalog,
        CancellationToken CancellationToken);

    private sealed record GroupIdentity(long? FlowId, long? NormalizedId, string Name)
    {
        internal bool IsUsable => FlowId is > 0 || NormalizedId is > 0 || Name.Length > 0;
        internal string Key => FlowId is > 0 ? $"f:{FlowId}" :
            NormalizedId is > 0 ? $"n:{NormalizedId}" : $"s:{Name.ToUpperInvariant()}";
    }

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
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous);
            }
            if (matches.Count == 0)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
            }
            return matches[0];
        }
    }
}
