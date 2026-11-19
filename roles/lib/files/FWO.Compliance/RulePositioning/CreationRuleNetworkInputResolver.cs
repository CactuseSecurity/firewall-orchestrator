using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NetTools;
using CreationRuleNetworkResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleNetworkInput,
    FWO.Compliance.RulePositioning.CreationRuleNetworkResolutionFailureCategory>;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule addresses from ordered ticket and Flow state.</summary>
public sealed class CreationRuleNetworkInputResolver(IFlowGroupResolver flowGroupResolver)
    : ICreationRuleNetworkInputResolver
{
    private const int kMaximumGroupDepth = 100;
    private readonly IFlowGroupResolver flowGroupResolver = flowGroupResolver ??
        throw new ArgumentNullException(nameof(flowGroupResolver));

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
            TicketObjectReferenceIndex referenceIndex = TicketObjectReferenceIndex.Create(
                accessTask,
                ticketTasks);
            List<WfReqElement> sources = ActiveSide(accessTask, ElemFieldType.source);
            List<WfReqElement> destinations = ActiveSide(accessTask, ElemFieldType.destination);
            if (sources.Count == 0 || destinations.Count == 0)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }

            CatalogIndex catalog = await LoadCatalogAsync(
                sources.Concat(destinations), referenceIndex, cancellationToken);
            Dictionary<long, GroupExpansionResult> wholeGroupCache = [];
            ResolutionContext context = new(
                referenceIndex, catalog, wholeGroupCache, cancellationToken);
            List<IPAddressRange> sourceRanges = ResolveElements(sources, context);
            List<IPAddressRange> destinationRanges = ResolveElements(destinations, context);
            return CreationRuleNetworkResolutionResult.Success(new CreationRuleNetworkInput(
                PositioningAddressRangeNormalizer.Distinct(sourceRanges),
                PositioningAddressRangeNormalizer.Distinct(destinationRanges)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NetworkResolutionException exception)
        {
            return CreationRuleNetworkResolutionResult.Failure(exception.Category);
        }
        catch (Exception)
        {
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

    /// <summary>Loads existing groups and exact referenced group-modification baselines.</summary>
    private async Task<CatalogIndex> LoadCatalogAsync(
        IEnumerable<WfReqElement> accessElements,
        TicketObjectReferenceIndex referenceIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            List<FlowNetworkGroupResolution> idGroups = [];
            List<FlowNetworkGroupResolution> nameGroups = [];
            HashSet<long> queriedIds = [];
            HashSet<string> queriedNames = new(StringComparer.OrdinalIgnoreCase);
            for (int iteration = 0; iteration < kMaximumGroupDepth; iteration++)
            {
                CatalogIndex catalog = new(ReconcileCatalogGroups(idGroups, nameGroups));
                CatalogQueryState queryState = new(catalog, queriedIds, queriedNames);
                List<GroupIdentity> requested = CatalogRequestsForElements(
                    accessElements,
                    referenceIndex,
                    queryState,
                    cancellationToken);
                List<long> newIds = requested.Where(reference => reference.FlowId is > 0)
                    .Select(reference => reference.FlowId!.Value)
                    .Where(id => !queriedIds.Contains(id)).Distinct().ToList();
                List<string> newNames = requested.Where(reference =>
                        reference.FlowId is not > 0 && reference.Name.Length > 0)
                    .Select(reference => reference.Name)
                    .Where(name => !queriedNames.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (newIds.Count == 0 && newNames.Count == 0)
                {
                    return catalog;
                }
                await LoadNewCatalogGroupsAsync(
                    newIds, newNames, idGroups, nameGroups, queriedIds, queriedNames, cancellationToken);
            }
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
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

    /// <summary>Loads newly discovered ID and name selectors in bounded chunks.</summary>
    private async Task LoadNewCatalogGroupsAsync(
        List<long> newIds,
        List<string> newNames,
        List<FlowNetworkGroupResolution> idGroups,
        List<FlowNetworkGroupResolution> nameGroups,
        HashSet<long> queriedIds,
        HashSet<string> queriedNames,
        CancellationToken cancellationToken)
    {
        foreach (List<long> chunk in newIds.Chunk(FlowGroupResolutionParameters.MaxSelectors)
            .Select(values => values.ToList()))
        {
            idGroups.AddRange(await LoadCatalogChunkAsync(
                new FlowGroupResolutionParameters { NetworkGroupIds = chunk }, cancellationToken));
            queriedIds.UnionWith(chunk);
        }
        foreach (List<string> chunk in newNames.Chunk(FlowGroupResolutionParameters.MaxSelectors)
            .Select(values => values.ToList()))
        {
            nameGroups.AddRange(await LoadCatalogChunkAsync(
                new FlowGroupResolutionParameters { NetworkGroupNames = chunk }, cancellationToken));
            queriedNames.UnionWith(chunk);
        }
    }

    /// <summary>Coalesces identical snapshots returned once by ID and once by name.</summary>
    private static List<FlowNetworkGroupResolution> ReconcileCatalogGroups(
        List<FlowNetworkGroupResolution> idGroups,
        List<FlowNetworkGroupResolution> nameGroups)
    {
        List<FlowNetworkGroupResolution> groups = [.. idGroups];
        List<FlowNetworkGroupResolution> unmatchedIdGroups = [.. idGroups];
        foreach (FlowNetworkGroupResolution nameGroup in nameGroups)
        {
            int equivalentIndex = unmatchedIdGroups.FindIndex(idGroup =>
                EquivalentCatalogGroups(idGroup, nameGroup));
            if (equivalentIndex >= 0)
            {
                unmatchedIdGroups.RemoveAt(equivalentIndex);
            }
            else
            {
                groups.Add(nameGroup);
            }
        }
        return groups;
    }

    /// <summary>Checks whether ID and name queries returned the same catalog group snapshot.</summary>
    private static bool EquivalentCatalogGroups(
        FlowNetworkGroupResolution left,
        FlowNetworkGroupResolution right)
    {
        if (left.Id != right.Id || !SameName(left.Name, right.Name) ||
            left.Members.Count != right.Members.Count)
        {
            return false;
        }
        List<(long Id, string Name, string Start, string End)> leftMembers = left.Members
            .Select(member => (member.Id, member.Name, member.IpStart, member.IpEnd))
            .OrderBy(member => member.Id)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ThenBy(member => member.IpStart, StringComparer.Ordinal)
            .ThenBy(member => member.IpEnd, StringComparer.Ordinal)
            .ToList();
        List<(long Id, string Name, string Start, string End)> rightMembers = right.Members
            .Select(member => (member.Id, member.Name, member.IpStart, member.IpEnd))
            .OrderBy(member => member.Id)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ThenBy(member => member.IpStart, StringComparer.Ordinal)
            .ThenBy(member => member.IpEnd, StringComparer.Ordinal)
            .ToList();
        return leftMembers.SequenceEqual(rightMembers);
    }

    /// <summary>Collects existing groups and referenced group-modification baselines.</summary>
    private static List<GroupIdentity> CatalogRequestsForElements(
        IEnumerable<WfReqElement> accessElements,
        TicketObjectReferenceIndex referenceIndex,
        CatalogQueryState queryState,
        CancellationToken cancellationToken)
    {
        List<GroupIdentity> requested = [];
        HashSet<long> groupPath = [];
        Dictionary<long, GroupTraversalSummary> completedWholeGroups = [];
        CatalogTraversalContext context = new(
            referenceIndex,
            queryState,
            requested,
            groupPath,
            completedWholeGroups,
            cancellationToken);
        foreach (WfReqElement element in accessElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = CollectCatalogRequests(element, context, 0);
        }
        return requested;
    }

    /// <summary>Collects catalog groups reachable through one access or nested group element.</summary>
    private static GroupTraversalSummary CollectCatalogRequests(
        WfReqElement element,
        CatalogTraversalContext context,
        int depth)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.GroupPath.Count > 0 && HasAddress(element))
        {
            return EmptyTraversalSummary();
        }
        if (element.ObjectTaskId == null)
        {
            bool canRequestCatalogGroup = context.GroupPath.Count == 0
                ? GroupIdentityForElement(element).IsUsable
                : CanRepresentCatalogGroup(element);
            if (!HasAddress(element) && canRequestCatalogGroup)
            {
                context.Requested.Add(GroupIdentityForElement(element));
            }
            return EmptyTraversalSummary();
        }

        CreationRuleNetworkResolutionFailureCategory unresolvedCategory = context.GroupPath.Count > 0
            ? CreationRuleNetworkResolutionFailureCategory.GroupUnresolved
            : ReferenceFailureCategory(element);
        WfReqTask referencedTask = context.ReferenceIndex.Resolve(element, unresolvedCategory);
        if (IsNetworkGroupTask(referencedTask))
        {
            return CollectReferencedGroupCatalogRequests(element, referencedTask, context, depth);
        }
        return EmptyTraversalSummary();
    }

    /// <summary>Walks the exact ticket-group graph to find required catalog groups.</summary>
    private static GroupTraversalSummary CollectReferencedGroupCatalogRequests(
        WfReqElement reference,
        WfReqTask groupTask,
        CatalogTraversalContext context,
        int depth)
    {
        EnterGroup(groupTask, context.GroupPath, depth);
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            bool wholeGroup = ReferencesWholeGroup(reference, groupTask);
            if (wholeGroup && context.CompletedWholeGroups.TryGetValue(
                groupTask.Id, out GroupTraversalSummary? cachedSummary) &&
                CanReuseGroupTraversal(
                    cachedSummary.TraversedTaskIds,
                    cachedSummary.MaximumRelativeDepth,
                    groupTask,
                    context.GroupPath,
                    depth))
            {
                return cachedSummary;
            }
            List<WfReqElement> dependencyMembers;
            if (groupTask.TaskType == WfTaskType.group_create.ToString())
            {
                List<WfReqElement> members = CreatedGroupMembers(groupTask);
                dependencyMembers = SelectReferencedGroupMembers(reference, groupTask, members);
            }
            else if (groupTask.TaskType == WfTaskType.group_modify.ToString())
            {
                GroupIdentity identity = GroupIdentityForTask(groupTask);
                context.Requested.Add(identity);
                if (!WasCatalogGroupQueried(context.QueryState, identity))
                {
                    return SingleGroupTraversalSummary(groupTask.Id);
                }
                List<WfReqElement> members =
                    [.. CatalogMembersAsElements(context.QueryState.Catalog.Resolve(identity))];
                ApplyMemberActions(members, SourceMembers(groupTask));
                dependencyMembers = SelectReferencedGroupMembers(reference, groupTask, members);
            }
            else
            {
                dependencyMembers = [];
            }
            HashSet<long> traversedTaskIds = [groupTask.Id];
            int maximumRelativeDepth = 0;
            foreach (WfReqElement member in dependencyMembers)
            {
                GroupTraversalSummary nestedSummary = CollectCatalogRequests(
                    member, context, depth + 1);
                traversedTaskIds.UnionWith(nestedSummary.TraversedTaskIds);
                if (nestedSummary.TraversedTaskIds.Count > 0)
                {
                    maximumRelativeDepth = Math.Max(
                        maximumRelativeDepth, nestedSummary.MaximumRelativeDepth + 1);
                }
            }
            GroupTraversalSummary summary = new(traversedTaskIds, maximumRelativeDepth);
            if (wholeGroup)
            {
                context.CompletedWholeGroups[groupTask.Id] = summary;
            }
            return summary;
        }
        finally
        {
            context.GroupPath.Remove(groupTask.Id);
        }
    }

    /// <summary>Rejects addressless object snapshots as catalog-group selectors.</summary>
    private static bool CanRepresentCatalogGroup(WfReqElement element) =>
        element.FlowNetworkGroupId is > 0 ||
        element.FlowNetworkObjectId is not > 0 &&
        element.NetworkId is not > 0 &&
        !string.IsNullOrWhiteSpace(element.GroupName) &&
        (string.IsNullOrWhiteSpace(element.Name) || SameName(element.GroupName, element.Name));

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

    /// <summary>Resolves exact references, persisted leaves, and existing catalog groups.</summary>
    private static List<IPAddressRange> ResolveElements(
        IEnumerable<WfReqElement> elements,
        ResolutionContext context)
    {
        List<IPAddressRange> ranges = [];
        foreach (WfReqElement element in elements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (element.ObjectTaskId != null)
            {
                ranges.AddRange(ResolveReferencedElement(element, context));
            }
            else if (HasAddress(element))
            {
                ranges.Add(PositioningAddressRangeNormalizer.Normalize(element));
            }
            else
            {
                ranges.AddRange(ResolveCatalogGroup(GroupIdentityForElement(element), context));
            }
        }
        return ranges;
    }

    /// <summary>Resolves the exact object or group task referenced by one access element.</summary>
    private static List<IPAddressRange> ResolveReferencedElement(
        WfReqElement element,
        ResolutionContext context)
    {
        CreationRuleNetworkResolutionFailureCategory unresolvedCategory = ReferenceFailureCategory(element);
        WfReqTask referencedTask = context.ReferenceIndex.Resolve(element, unresolvedCategory);
        if (IsNetworkGroupTask(referencedTask))
        {
            return ResolveReferencedGroup(element, referencedTask, context);
        }
        return [ResolveReferencedObject(referencedTask)];
    }

    /// <summary>Resolves one exact network object task's requested state.</summary>
    private static IPAddressRange ResolveReferencedObject(WfReqTask objectTask)
    {
        if (!IsNetworkObjectTask(objectTask) || !WfObjectTaskHelper.HasValidElementStructure(objectTask))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
        }
        WfReqElement? requested = WfObjectTaskHelper.GetRequestedElement(objectTask);
        if (requested == null || WfObjectTaskHelper.GetKind(requested) != ObjectTaskKind.Network)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
        }
        return PositioningAddressRangeNormalizer.Normalize(requested);
    }

    /// <summary>Resolves an existing group that has no ticket-local reference.</summary>
    private static List<IPAddressRange> ResolveCatalogGroup(
        GroupIdentity identity,
        ResolutionContext context)
    {
        if (!identity.IsUsable)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
        }
        return ResolveCatalogMembers(context.Catalog.Resolve(identity), context.CancellationToken);
    }

    /// <summary>Resolves one exact referenced group task.</summary>
    private static List<IPAddressRange> ResolveReferencedGroup(
        WfReqElement reference,
        WfReqTask groupTask,
        ResolutionContext context)
    {
        HashSet<long> groupPath = [];
        return ResolveReferencedGroupExpansion(
            reference, groupTask, context, groupPath, 0).Ranges;
    }

    /// <summary>Resolves one exact referenced group within a bounded ticket-group path.</summary>
    private static GroupExpansionResult ResolveReferencedGroupExpansion(
        WfReqElement reference,
        WfReqTask groupTask,
        ResolutionContext context,
        HashSet<long> groupPath,
        int depth)
    {
        EnterGroup(groupTask, groupPath, depth);
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            bool wholeGroup = ReferencesWholeGroup(reference, groupTask);
            if (wholeGroup && context.WholeGroupCache.TryGetValue(
                groupTask.Id, out GroupExpansionResult? cachedResult) &&
                CanReuseGroupExpansion(cachedResult, groupTask, groupPath, depth))
            {
                return cachedResult;
            }
            List<WfReqElement> members;
            if (groupTask.TaskType == WfTaskType.group_create.ToString())
            {
                members = CreatedGroupMembers(groupTask);
            }
            else if (groupTask.TaskType == WfTaskType.group_modify.ToString())
            {
                members = ModifiedGroupMembers(groupTask, context);
            }
            else
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
            }
            List<WfReqElement> selectedMembers = SelectReferencedGroupMembers(reference, groupTask, members);
            GroupExpansionResult memberResult = ResolvePersistedGroupMembers(
                selectedMembers, context, groupPath, depth + 1);
            List<IPAddressRange> ranges = wholeGroup
                ? PositioningAddressRangeNormalizer.Distinct(memberResult.Ranges)
                : memberResult.Ranges;
            HashSet<long> traversedTaskIds = [groupTask.Id, .. memberResult.TraversedTaskIds];
            GroupExpansionResult result = new(
                ranges, traversedTaskIds, memberResult.MaximumRelativeDepth);
            if (wholeGroup)
            {
                context.WholeGroupCache[groupTask.Id] = result;
            }
            return result;
        }
        finally
        {
            groupPath.Remove(groupTask.Id);
        }
    }

    /// <summary>Builds one referenced new group's effective persisted members.</summary>
    private static List<WfReqElement> CreatedGroupMembers(WfReqTask groupTask)
    {
        List<WfReqElement> members = [];
        ApplyMemberActions(members, SourceMembers(groupTask));
        return members;
    }

    /// <summary>Applies one referenced group modification to its catalog baseline members.</summary>
    private static List<WfReqElement> ModifiedGroupMembers(
        WfReqTask groupTask,
        ResolutionContext context)
    {
        GroupIdentity identity = GroupIdentityForTask(groupTask);
        if (!identity.IsUsable)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
        List<WfReqElement> members = [.. CatalogMembersAsElements(context.Catalog.Resolve(identity))];
        ApplyMemberActions(members, SourceMembers(groupTask));
        return members;
    }

    /// <summary>Selects either the referenced group itself or one exact active member.</summary>
    private static List<WfReqElement> SelectReferencedGroupMembers(
        WfReqElement reference,
        WfReqTask groupTask,
        List<WfReqElement> members)
    {
        if (ReferencesWholeGroup(reference, groupTask))
        {
            return members;
        }

        List<WfReqElement> matches = members.Where(member =>
            MatchesMemberReference(reference, member, groupTask)).ToList();
        if (matches.Count != 1)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
        return [matches[0]];
    }

    /// <summary>Checks the strongest supplied identity against the exact referenced group task.</summary>
    private static bool ReferencesWholeGroup(WfReqElement reference, WfReqTask groupTask)
    {
        if (reference.FlowNetworkObjectId is > 0 || HasAddress(reference))
        {
            return false;
        }
        GroupIdentity referenceIdentity = GroupIdentityForElement(reference);
        GroupIdentity taskIdentity = GroupIdentityForTask(groupTask);
        if (!referenceIdentity.IsUsable &&
            reference.FlowNetworkObjectId is not > 0 &&
            !HasAddress(reference))
        {
            return true;
        }
        if (referenceIdentity.FlowId is > 0)
        {
            return referenceIdentity.FlowId == taskIdentity.FlowId;
        }
        if (referenceIdentity.NormalizedId is > 0)
        {
            return referenceIdentity.NormalizedId == taskIdentity.NormalizedId;
        }
        if (!string.IsNullOrWhiteSpace(reference.Name))
        {
            return SameName(reference.Name, taskIdentity.Name);
        }
        return SameName(reference.GroupName, taskIdentity.Name);
    }

    /// <summary>Matches one access reference to one active member without sibling-task inference.</summary>
    private static bool MatchesMemberReference(
        WfReqElement reference,
        WfReqElement member,
        WfReqTask groupTask)
    {
        if (reference.FlowNetworkObjectId is > 0)
        {
            return reference.FlowNetworkObjectId == member.FlowNetworkObjectId;
        }
        if (reference.FlowNetworkGroupId is > 0)
        {
            return reference.FlowNetworkGroupId == member.FlowNetworkGroupId;
        }
        if (reference.NetworkId is > 0)
        {
            return reference.NetworkId == member.NetworkId;
        }
        if (HasAddress(reference))
        {
            return HasSameAddress(reference, member);
        }
        string taskName = GroupIdentityForTask(groupTask).Name;
        if (!string.IsNullOrWhiteSpace(reference.GroupName) &&
            !SameName(reference.GroupName, taskName))
        {
            return SameName(reference.GroupName, member.GroupName);
        }
        return !string.IsNullOrWhiteSpace(reference.Name) &&
            !SameName(reference.Name, taskName) &&
            SameName(reference.Name, member.Name);
    }

    /// <summary>Compares two valid concrete address ranges.</summary>
    private static bool HasSameAddress(WfReqElement left, WfReqElement right)
    {
        if (!HasAddress(left) || !HasAddress(right))
        {
            return false;
        }
        try
        {
            IPAddressRange leftRange = PositioningAddressRangeNormalizer.Normalize(left);
            IPAddressRange rightRange = PositioningAddressRangeNormalizer.Normalize(right);
            return leftRange.Begin.Equals(rightRange.Begin) && leftRange.End.Equals(rightRange.End);
        }
        catch (NetworkResolutionException)
        {
            return false;
        }
    }

    /// <summary>Returns the persisted address-member actions of one network group task.</summary>
    private static IEnumerable<WfReqElement> SourceMembers(WfReqTask groupTask) =>
        groupTask.Elements.Where(element => element.Field == ElemFieldType.source.ToString());

    /// <summary>Resolves active persisted members through exact nested group references.</summary>
    private static GroupExpansionResult ResolvePersistedGroupMembers(
        List<WfReqElement> members,
        ResolutionContext context,
        HashSet<long> groupPath,
        int depth)
    {
        if (members.Count == 0)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
        List<IPAddressRange> ranges = [];
        HashSet<long> traversedTaskIds = [];
        int maximumRelativeDepth = 0;
        foreach (WfReqElement member in members)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (HasAddress(member))
            {
                ranges.Add(NormalizeGroupMember(member));
            }
            else if (member.ObjectTaskId != null)
            {
                WfReqTask referencedTask = context.ReferenceIndex.Resolve(
                    member,
                    CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
                if (!IsNetworkGroupTask(referencedTask))
                {
                    throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
                }
                GroupExpansionResult nestedResult = ResolveReferencedGroupExpansion(
                    member, referencedTask, context, groupPath, depth);
                ranges.AddRange(nestedResult.Ranges);
                traversedTaskIds.UnionWith(nestedResult.TraversedTaskIds);
                maximumRelativeDepth = Math.Max(
                    maximumRelativeDepth, nestedResult.MaximumRelativeDepth + 1);
            }
            else if (CanRepresentCatalogGroup(member))
            {
                ranges.AddRange(ResolveCatalogGroup(GroupIdentityForElement(member), context));
            }
            else
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
            }
        }
        return new GroupExpansionResult(ranges, traversedTaskIds, maximumRelativeDepth);
    }

    /// <summary>Normalizes one concrete group member as a group-resolution failure on error.</summary>
    private static IPAddressRange NormalizeGroupMember(WfReqElement member)
    {
        try
        {
            return PositioningAddressRangeNormalizer.Normalize(member);
        }
        catch (NetworkResolutionException)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
    }

    /// <summary>Enters one bounded group path and rejects cycles.</summary>
    private static void EnterGroup(WfReqTask groupTask, HashSet<long> groupPath, int depth)
    {
        if (depth >= kMaximumGroupDepth || !groupPath.Add(groupTask.Id))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
    }

    /// <summary>Checks cached descendants against active ancestors and remaining depth.</summary>
    private static bool CanReuseGroupExpansion(
        GroupExpansionResult cachedResult,
        WfReqTask groupTask,
        HashSet<long> groupPath,
        int depth) =>
        CanReuseGroupTraversal(
            cachedResult.TraversedTaskIds,
            cachedResult.MaximumRelativeDepth,
            groupTask,
            groupPath,
            depth);

    /// <summary>Checks cached traversal metadata against active ancestors and remaining depth.</summary>
    private static bool CanReuseGroupTraversal(
        IReadOnlyCollection<long> traversedTaskIds,
        int maximumRelativeDepth,
        WfReqTask groupTask,
        HashSet<long> groupPath,
        int depth)
    {
        if (depth + maximumRelativeDepth >= kMaximumGroupDepth)
        {
            return false;
        }
        return !traversedTaskIds.Any(taskId =>
            taskId != groupTask.Id && groupPath.Contains(taskId));
    }

    /// <summary>Creates traversal metadata for one element without a nested ticket group.</summary>
    private static GroupTraversalSummary EmptyTraversalSummary()
    {
        HashSet<long> traversedTaskIds = [];
        return new GroupTraversalSummary(traversedTaskIds, 0);
    }

    /// <summary>Creates traversal metadata containing only one deferred group task.</summary>
    private static GroupTraversalSummary SingleGroupTraversalSummary(long groupTaskId)
    {
        HashSet<long> traversedTaskIds = [groupTaskId];
        return new GroupTraversalSummary(traversedTaskIds, 0);
    }

    /// <summary>Checks whether the catalog selector for one group identity has been queried.</summary>
    private static bool WasCatalogGroupQueried(
        CatalogQueryState queryState,
        GroupIdentity identity) =>
        identity.FlowId is > 0
            ? queryState.QueriedIds.Contains(identity.FlowId.Value)
            : identity.Name.Length > 0 && queryState.QueriedNames.Contains(identity.Name);
    /// <summary>Applies supported group member actions with identity-based replacement/removal.</summary>
    private static void ApplyMemberActions(List<WfReqElement> members, IEnumerable<WfReqElement> changes)
    {
        foreach (WfReqElement change in changes)
        {
            if (!Enum.TryParse(change.RequestAction, false, out RequestAction action))
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
            }
            List<int> matches = MemberMatchIndexes(members, change, action);
            if (matches.Count > 1 && action is RequestAction.modify or RequestAction.delete)
            {
                throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous);
            }
            ApplyMemberAction(members, change, action, matches);
        }
    }
    /// <summary>Finds members matching the strongest identity supplied by one member change.</summary>
    private static List<int> MemberMatchIndexes(
        List<WfReqElement> members,
        WfReqElement change,
        RequestAction action)
    {
        List<int> matches = Enumerable.Range(0, members.Count).Where(index =>
            MatchesMemberChange(change, members[index])).ToList();
        if (matches.Count == 0 &&
            action == RequestAction.modify &&
            !HasStableMemberIdentity(change) &&
            !string.IsNullOrWhiteSpace(change.Name))
        {
            matches = Enumerable.Range(0, members.Count).Where(index =>
                SameName(change.Name, members[index].Name)).ToList();
        }
        return matches;
    }
    /// <summary>Matches one member action without treating a parent group name as object identity.</summary>
    private static bool MatchesMemberChange(WfReqElement change, WfReqElement member)
    {
        if (change.FlowNetworkObjectId is > 0)
        {
            return change.FlowNetworkObjectId == member.FlowNetworkObjectId;
        }
        if (change.FlowNetworkGroupId is > 0)
        {
            return change.FlowNetworkGroupId == member.FlowNetworkGroupId;
        }
        if (change.NetworkId is > 0)
        {
            return change.NetworkId == member.NetworkId;
        }
        if (change.ObjectTaskId is > 0 &&
            !HasAddress(change) &&
            string.IsNullOrWhiteSpace(change.Name))
        {
            return change.ObjectTaskId == member.ObjectTaskId;
        }
        if (HasAddress(change))
        {
            return HasSameAddress(change, member);
        }
        if (CanRepresentCatalogGroup(change))
        {
            return SameName(change.GroupName, member.GroupName);
        }
        if (!string.IsNullOrWhiteSpace(change.Name))
        {
            return SameName(change.Name, member.Name);
        }
        return false;
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
                RequireSingleMemberMatch(matches);
                members[matches[0]] = Clone(change);
                return;
            case RequestAction.delete:
                RequireSingleMemberMatch(matches);
                members.RemoveAt(matches[0]);
                return;
            default:
                throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
    }
    /// <summary>Rejects a group member change without exactly one existing match.</summary>
    private static void RequireSingleMemberMatch(List<int> matches)
    {
        if (matches.Count != 1)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved);
        }
    }
    /// <summary>Normalizes the flattened address members of one catalog group.</summary>
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
    /// <summary>Checks whether a group member carries an authoritative persisted identity.</summary>
    private static bool HasStableMemberIdentity(WfReqElement element) => element.FlowNetworkObjectId is > 0 || element.FlowNetworkGroupId is > 0 || element.NetworkId is > 0;
    /// <summary>Checks whether a workflow element carries concrete address data.</summary>
    private static bool HasAddress(WfReqElement element) =>
        !string.IsNullOrWhiteSpace(element.IpString) || element.Cidr?.Valid == true;
    /// <summary>Selects the typed unresolved category implied by one persisted access element.</summary>
    private static CreationRuleNetworkResolutionFailureCategory ReferenceFailureCategory(
        WfReqElement element) =>
        !HasAddress(element) && GroupIdentityForElement(element).IsUsable
            ? CreationRuleNetworkResolutionFailureCategory.GroupUnresolved
            : CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved;
    /// <summary>Extracts the prioritized identity of a ticket group task.</summary>
    private static GroupIdentity GroupIdentityForTask(WfReqTask task) => new(
        task.GetAddInfoLongValue(AdditionalInfoKeys.GroupId),
        task.NetworkGroupId,
        FirstName(task.GetAddInfoValue(AdditionalInfoKeys.GrpName), task.Title));
    /// <summary>Extracts the prioritized group identity of a workflow element.</summary>
    private static GroupIdentity GroupIdentityForElement(WfReqElement element) => new(
        element.FlowNetworkGroupId,
        element.NetworkId,
        FirstName(element.GroupName, element.Name));
    /// <summary>Returns the first normalized nonblank identity name.</summary>
    private static string FirstName(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first.Trim() : second?.Trim() ?? string.Empty;
    /// <summary>Compares two nonblank names without case sensitivity.</summary>
    private static bool SameName(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    /// <summary>Copies a workflow element before retaining it in projected state.</summary>
    private static WfReqElement Clone(WfReqElement element) => new(element);
    /// <summary>Creates an internal typed control-flow failure.</summary>
    private static NetworkResolutionException Failure(CreationRuleNetworkResolutionFailureCategory category) => new(category);
    /// <summary>Immutable state shared while resolving exact references and catalog groups.</summary>
    private sealed record ResolutionContext(
        TicketObjectReferenceIndex ReferenceIndex,
        CatalogIndex Catalog,
        Dictionary<long, GroupExpansionResult> WholeGroupCache,
        CancellationToken CancellationToken);
    /// <summary>One resolved group plus the descendants needed to validate safe cache reuse.</summary>
    private sealed record GroupExpansionResult(
        List<IPAddressRange> Ranges,
        HashSet<long> TraversedTaskIds,
        int MaximumRelativeDepth);
    /// <summary>Ticket-group descendants and maximum depth discovered during catalog preloading.</summary>
    private sealed record GroupTraversalSummary(
        HashSet<long> TraversedTaskIds,
        int MaximumRelativeDepth);
    /// <summary>Catalog snapshot and selectors already queried during iterative discovery.</summary>
    private sealed record CatalogQueryState(
        CatalogIndex Catalog,
        HashSet<long> QueriedIds,
        HashSet<string> QueriedNames);
    /// <summary>Mutable request-local state used while discovering catalog group dependencies.</summary>
    private sealed record CatalogTraversalContext(
        TicketObjectReferenceIndex ReferenceIndex,
        CatalogQueryState QueryState,
        List<GroupIdentity> Requested,
        HashSet<long> GroupPath,
        Dictionary<long, GroupTraversalSummary> CompletedWholeGroups,
        CancellationToken CancellationToken);
    /// <summary>Prioritized identities used to match one network group.</summary>
    private sealed record GroupIdentity(long? FlowId, long? NormalizedId, string Name)
    {
        /// <summary>Gets whether at least one stable group identity is available.</summary>
        internal bool IsUsable => FlowId is > 0 || NormalizedId is > 0 || Name.Length > 0;
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
