using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NetTools;
using System.Net.Sockets;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves a new access rule's workflow elements to ordered zone sets.</summary>
public sealed class CreationRuleZoneResolver(IFlowGroupResolver flowGroupResolver, GlobalConfig globalConfig) : ICreationRuleZoneResolver
{
    private sealed record ActiveElement(NwObjectElement NetworkElement, WfReqElement RequestElement);
    private sealed record GroupReference(long? Id, string? Name);
    private sealed record SideResolution(List<PositioningZoneReference> Zones, ZoneCoverage Coverage);

    private sealed class ResolutionFailureException(CreationRuleZoneResolutionFailureCategory category) : Exception
    {
        public CreationRuleZoneResolutionFailureCategory Category { get; } = category;
    }

    /// <inheritdoc />
    public async Task<CreationRuleZoneResolutionResult> ResolveAsync(
        WfReqTask requestTask,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSupportedTask(requestTask);
            List<ComplianceNetworkZone> validatedZones = ValidateZones(networkZones, cancellationToken);
            (List<ActiveElement> sourceElements, List<ActiveElement> destinationElements) = GetActiveElements(requestTask, cancellationToken);
            Dictionary<long, FlowNetworkGroupResolution> groupsById = await ResolveGroupsAsync(
                sourceElements.Concat(destinationElements), cancellationToken);
            List<IPAddressRange> sourceRanges = NormalizeElements(sourceElements, groupsById, cancellationToken);
            List<IPAddressRange> destinationRanges = NormalizeElements(destinationElements, groupsById, cancellationToken);

            if (sourceRanges.Count == 0 || destinationRanges.Count == 0)
            {
                return Failure(CreationRuleZoneResolutionFailureCategory.InvalidInput);
            }

            SideResolution source = ResolveSide(sourceRanges, validatedZones, cancellationToken);
            SideResolution destination = ResolveSide(destinationRanges, validatedZones, cancellationToken);
            return new CreationRuleZoneResolutionSuccess(new CreationRuleZoneInput(
                globalConfig.DesignatedZoneMatrixId,
                source.Zones,
                destination.Zones,
                source.Coverage,
                destination.Coverage));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ResolutionFailureException exception)
        {
            return Failure(exception.Category);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            return Failure(CreationRuleZoneResolutionFailureCategory.InvalidAddress);
        }
        catch (Exception)
        {
            return Failure(CreationRuleZoneResolutionFailureCategory.UnexpectedFailure);
        }
    }

    /// <inheritdoc />
    public Task<CreationRuleZoneResolutionResult> ResolveAsync(
        CreationRuleNetworkInput networkInput,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (networkInput == null)
            {
                return Task.FromResult<CreationRuleZoneResolutionResult>(
                    Failure(CreationRuleZoneResolutionFailureCategory.InvalidInput));
            }
            List<ComplianceNetworkZone> validatedZones = ValidateZones(networkZones, cancellationToken);
            SideResolution source = ResolveSide([.. networkInput.SourceRanges], validatedZones, cancellationToken);
            SideResolution destination = ResolveSide([.. networkInput.DestinationRanges], validatedZones, cancellationToken);
            return Task.FromResult<CreationRuleZoneResolutionResult>(new CreationRuleZoneResolutionSuccess(
                new CreationRuleZoneInput(
                    globalConfig.DesignatedZoneMatrixId,
                    source.Zones,
                    destination.Zones,
                    source.Coverage,
                    destination.Coverage)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ResolutionFailureException exception)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(Failure(exception.Category));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(
                Failure(CreationRuleZoneResolutionFailureCategory.InvalidAddress));
        }
        catch (Exception)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(
                Failure(CreationRuleZoneResolutionFailureCategory.UnexpectedFailure));
        }
    }

    /// <summary>Rejects tasks outside access-rule creation.</summary>
    private static void ValidateSupportedTask(WfReqTask requestTask)
    {
        if (requestTask == null || requestTask.Elements == null)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidInput);
        }

        if (!string.Equals(requestTask.TaskType, WfTaskType.access.ToString(), StringComparison.Ordinal)
            || !string.Equals(requestTask.RequestAction, RequestAction.create.ToString(), StringComparison.Ordinal))
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.UnsupportedTask);
        }
    }

    /// <summary>Validates and copies caller-supplied zone rows.</summary>
    private List<ComplianceNetworkZone> ValidateZones(
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken)
    {
        if (globalConfig.DesignatedZoneMatrixId <= 0 || networkZones == null || networkZones.Count == 0)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
        }

        HashSet<int> zoneIds = [];
        List<ComplianceNetworkZone> validatedZones = [];
        foreach (ComplianceNetworkZone? zone in networkZones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (zone == null || zone.Id <= 0 || !zoneIds.Add(zone.Id)
                || zone.CriterionId != globalConfig.DesignatedZoneMatrixId || string.IsNullOrWhiteSpace(zone.Name)
                || zone.IPRanges == null)
            {
                throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
            }

            validatedZones.Add(CloneZone(zone, cancellationToken));
        }

        ValidateAutomaticInternetZone(validatedZones, cancellationToken);
        return validatedZones;
    }

    /// <summary>Validates the configured automatic Internet-zone invariant.</summary>
    private void ValidateAutomaticInternetZone(
        List<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken)
    {
        if (!globalConfig.AutoCalculateInternetZone)
        {
            return;
        }

        List<ComplianceNetworkZone> internetZones = [.. networkZones.Where(zone => zone.IsAutoCalculatedInternetZone)];
        bool containsIpv4Range = false;
        if (internetZones.Count == 1)
        {
            foreach (IPAddressRange range in internetZones[0].IPRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                containsIpv4Range |= range.Begin.AddressFamily == AddressFamily.InterNetwork;
            }
        }

        if (internetZones.Count != 1 || !containsIpv4Range)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
        }
    }

    /// <summary>Returns participating, active workflow elements for each side.</summary>
    private static (List<ActiveElement> Source, List<ActiveElement> Destination) GetActiveElements(
        WfReqTask requestTask,
        CancellationToken cancellationToken)
    {
        List<ActiveElement> source = GetActiveElements(requestTask, ElemFieldType.source, cancellationToken);
        List<ActiveElement> destination = GetActiveElements(requestTask, ElemFieldType.destination, cancellationToken);
        if (source.Count == 0 || destination.Count == 0)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidInput);
        }

        return (source, destination);
    }

    /// <summary>Pairs original request elements with their established normalized workflow representation.</summary>
    private static List<ActiveElement> GetActiveElements(
        WfReqTask requestTask,
        ElemFieldType field,
        CancellationToken cancellationToken)
    {
        List<WfReqElement> requestElements = [.. requestTask.Elements.Where(element =>
            string.Equals(element.Field, field.ToString(), StringComparison.Ordinal))];
        List<NwObjectElement> networkElements = requestTask.GetNwObjectElements(field);
        List<ActiveElement> activeElements = [];

        for (int index = 0; index < requestElements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string action = requestElements[index].RequestAction;
            if (string.Equals(action, RequestAction.delete.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsActiveAction(action))
            {
                throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidInput);
            }

            activeElements.Add(new ActiveElement(networkElements[index], requestElements[index]));
        }

        return activeElements;
    }

    /// <summary>Checks whether an element action participates in a creation rule.</summary>
    private static bool IsActiveAction(string action)
    {
        return string.Equals(action, RequestAction.create.ToString(), StringComparison.Ordinal)
            || string.Equals(action, RequestAction.unchanged.ToString(), StringComparison.Ordinal)
            || string.Equals(action, RequestAction.modify.ToString(), StringComparison.Ordinal)
            || string.Equals(action, RequestAction.addAfterCreation.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Resolves all distinct group references and returns groups by identity.</summary>
    private async Task<Dictionary<long, FlowNetworkGroupResolution>> ResolveGroupsAsync(
        IEnumerable<ActiveElement> elements,
        CancellationToken cancellationToken)
    {
        List<GroupReference> references = elements.Select(ToGroupReference).OfType<GroupReference>().ToList();
        List<long> ids = references.Where(reference => reference.Id.HasValue)
            .Select(reference => reference.Id!.Value).Distinct().ToList();
        List<string> names = references.Where(reference => !reference.Id.HasValue)
            .Select(reference => reference.Name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Dictionary<long, FlowNetworkGroupResolution> groupsById = [];

        foreach (List<long> chunk in ids.Chunk(FlowGroupResolutionParameters.MaxSelectors).Select(values => values.ToList()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlowGroupResolutionResult result = await LoadGroupsAsync(new() { NetworkGroupIds = chunk }, cancellationToken);
            ValidateIdResults(chunk, result.NetworkGroups, groupsById);
        }

        foreach (List<string> chunk in names.Chunk(FlowGroupResolutionParameters.MaxSelectors).Select(values => values.ToList()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlowGroupResolutionResult result = await LoadGroupsAsync(new() { NetworkGroupNames = chunk }, cancellationToken);
            ValidateNameResults(chunk, result.NetworkGroups, groupsById);
        }

        return groupsById;
    }

    /// <summary>Loads one selector chunk and safely maps query failures.</summary>
    private async Task<FlowGroupResolutionResult> LoadGroupsAsync(
        FlowGroupResolutionParameters parameters,
        CancellationToken cancellationToken)
    {
        FlowGroupResolutionResult? result;
        try
        {
            result = await flowGroupResolver.ResolveFlowGroupMembersAsync(parameters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.GroupQueryFailed);
        }

        return result ?? throw GroupUnresolved();
    }

    /// <summary>Validates ID-based results and adds them by identity.</summary>
    private static void ValidateIdResults(
        List<long> requestedIds,
        List<FlowNetworkGroupResolution>? groups,
        Dictionary<long, FlowNetworkGroupResolution> groupsById)
    {
        if (groups == null || groups.Count != requestedIds.Count)
        {
            throw GroupUnresolved();
        }

        foreach (FlowNetworkGroupResolution group in groups)
        {
            if (group == null || group.Id <= 0 || !requestedIds.Contains(group.Id) || !groupsById.TryAdd(group.Id, group))
            {
                throw GroupUnresolved();
            }

            ValidateGroupMembers(group);
        }
    }

    /// <summary>Validates unique case-insensitive name results and adds them by identity.</summary>
    private static void ValidateNameResults(
        List<string> requestedNames,
        List<FlowNetworkGroupResolution>? groups,
        Dictionary<long, FlowNetworkGroupResolution> groupsById)
    {
        if (groups == null || groups.Count != requestedNames.Count)
        {
            throw GroupUnresolved();
        }

        foreach (string requestedName in requestedNames)
        {
            List<FlowNetworkGroupResolution> matches = [.. groups.Where(group =>
                group != null && string.Equals(group.Name, requestedName, StringComparison.OrdinalIgnoreCase))];
            if (matches.Count != 1 || matches[0].Id <= 0 || !groupsById.TryAdd(matches[0].Id, matches[0]))
            {
                throw GroupUnresolved();
            }

            ValidateGroupMembers(matches[0]);
        }
    }

    /// <summary>Requires at least one technical member for every group.</summary>
    private static void ValidateGroupMembers(FlowNetworkGroupResolution group)
    {
        if (group.Members == null || group.Members.Count == 0)
        {
            throw GroupUnresolved();
        }
    }

    /// <summary>Normalizes direct addresses and expanded group members.</summary>
    private static List<IPAddressRange> NormalizeElements(
        List<ActiveElement> elements,
        Dictionary<long, FlowNetworkGroupResolution> groupsById,
        CancellationToken cancellationToken)
    {
        List<IPAddressRange> ranges = [];
        HashSet<string> rangeKeys = new(StringComparer.Ordinal);
        foreach (ActiveElement element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GroupReference? groupReference = ToGroupReference(element);
            if (groupReference != null)
            {
                FlowNetworkGroupResolution group = ResolveGroup(groupReference, groupsById);
                foreach (FlowNetworkMemberResolution? member in group.Members)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (member == null || (string.IsNullOrWhiteSpace(member.IpStart) && string.IsNullOrWhiteSpace(member.IpEnd)))
                    {
                        throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.UnsupportedObject);
                    }

                    AddNormalizedRange(member.IpStart, member.IpEnd, hasAddressInput: true, ranges, rangeKeys);
                }
            }
            else
            {
                bool hasAddressInput = !string.IsNullOrWhiteSpace(element.RequestElement.IpString)
                    || !string.IsNullOrWhiteSpace(element.RequestElement.IpEnd)
                    || element.RequestElement.Cidr?.Valid == true
                    || element.RequestElement.CidrEnd?.Valid == true;
                AddNormalizedRange(element.NetworkElement.IpString, element.NetworkElement.IpEndString,
                    hasAddressInput, ranges, rangeKeys);
            }
        }

        return ranges;
    }

    /// <summary>Returns the uniquely resolved group for one reference.</summary>
    private static FlowNetworkGroupResolution ResolveGroup(
        GroupReference reference,
        Dictionary<long, FlowNetworkGroupResolution> groupsById)
    {
        if (reference.Id.HasValue)
        {
            if (groupsById.TryGetValue(reference.Id.Value, out FlowNetworkGroupResolution? group))
            {
                return group;
            }
        }
        else
        {
            List<FlowNetworkGroupResolution> matches = [.. groupsById.Values.Where(group =>
                string.Equals(group.Name, reference.Name, StringComparison.OrdinalIgnoreCase))];
            if (matches.Count == 1)
            {
                return matches[0];
            }
        }

        throw GroupUnresolved();
    }

    /// <summary>Creates a group reference, preferring a positive ID over a name.</summary>
    private static GroupReference? ToGroupReference(ActiveElement element)
    {
        if (element.NetworkElement.FlowNetworkGroupId > 0)
        {
            return new GroupReference(element.NetworkElement.FlowNetworkGroupId.Value, null);
        }

        return string.IsNullOrWhiteSpace(element.NetworkElement.GroupName)
            ? null
            : new GroupReference(null, element.NetworkElement.GroupName);
    }

    /// <summary>Normalizes one address representation through the compliance parser.</summary>
    private static void AddNormalizedRange(
        string? start,
        string? end,
        bool hasAddressInput,
        List<IPAddressRange> ranges,
        HashSet<string> rangeKeys)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            throw new ResolutionFailureException(hasAddressInput
                ? CreationRuleZoneResolutionFailureCategory.InvalidAddress
                : CreationRuleZoneResolutionFailureCategory.UnsupportedObject);
        }

        try
        {
            NetworkObject networkObject = new()
            {
                IP = start,
                IpEnd = end ?? string.Empty,
                Type = new NetworkObjectType
                {
                    Name = string.IsNullOrWhiteSpace(end) ? ObjectType.Host : ObjectType.IPRange
                }
            };
            List<IPAddressRange> parsedRanges = ComplianceCheck.ParseIpRange(networkObject);
            if (parsedRanges.Count == 0)
            {
                throw InvalidAddress();
            }

            foreach (IPAddressRange range in parsedRanges)
            {
                if (!IsValidRange(range))
                {
                    throw InvalidAddress();
                }

                string key = $"{range.Begin.AddressFamily}:{range.Begin}-{range.End}";
                if (rangeKeys.Add(key))
                {
                    ranges.Add(new IPAddressRange(range.Begin, range.End));
                }
            }
        }
        catch (ResolutionFailureException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw InvalidAddress();
        }
    }

    /// <summary>Resolves and converts one side of the rule.</summary>
    private SideResolution ResolveSide(
        List<IPAddressRange> ranges,
        List<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken)
    {
        List<ComplianceNetworkZone> resolvedZones = ComplianceZoneResolver.ResolveZones(
            ranges,
            networkZones,
            globalConfig.AutoCalculateInternetZone,
            globalConfig.GetText("internet_local_zone"),
            cancellationToken,
            out List<IPAddressRange> unassignableRanges);
        List<PositioningZoneReference> references = resolvedZones
            .Select(ToPositioningReference)
            .OrderBy(reference => reference.Kind)
            .ThenBy(reference => reference.Id)
            .ToList();
        ZoneCoverage coverage = unassignableRanges.Count == 0
            ? ZoneCoverage.Complete
            : references.Count == 0 ? ZoneCoverage.None : ZoneCoverage.Partial;
        return new SideResolution(references, coverage);
    }

    /// <summary>Converts a compliance zone without retaining mutable caller data.</summary>
    private static PositioningZoneReference ToPositioningReference(ComplianceNetworkZone zone)
    {
        bool persisted = zone.Id > 0;
        return new PositioningZoneReference(
            persisted ? zone.Id : null,
            zone.IdString,
            zone.Name,
            zone.IsAutoCalculatedInternetZone,
            zone.IsAutoCalculatedUndefinedInternalZone,
            persisted ? PositioningZoneKind.Persisted : PositioningZoneKind.SyntheticInternetFallback);
    }

    /// <summary>Checks endpoint presence, family and ordering.</summary>
    private static bool IsValidRange(IPAddressRange? range)
    {
        return range?.Begin != null && range.End != null
            && range.Begin.AddressFamily == range.End.AddressFamily
            && IpOperations.CompareIpValues(range.Begin, range.End) <= 0;
    }

    /// <summary>Copies only zone state used by overlap resolution and positioning output.</summary>
    private static ComplianceNetworkZone CloneZone(ComplianceNetworkZone zone, CancellationToken cancellationToken)
    {
        List<IPAddressRange> ranges = [];
        foreach (IPAddressRange? range in zone.IPRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidRange(range))
            {
                throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
            }

            ranges.Add(new IPAddressRange(range!.Begin, range.End));
        }

        return new ComplianceNetworkZone
        {
            Id = zone.Id,
            IdString = zone.IdString,
            Name = zone.Name,
            CriterionId = zone.CriterionId,
            IsAutoCalculatedInternetZone = zone.IsAutoCalculatedInternetZone,
            IsAutoCalculatedUndefinedInternalZone = zone.IsAutoCalculatedUndefinedInternalZone,
            IPRanges = [.. ranges]
        };
    }

    /// <summary>Creates a failed result.</summary>
    private static CreationRuleZoneResolutionFailure Failure(CreationRuleZoneResolutionFailureCategory category)
    {
        return new CreationRuleZoneResolutionFailure(category);
    }

    /// <summary>Creates an unresolved-group control-flow exception.</summary>
    private static ResolutionFailureException GroupUnresolved()
    {
        return new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.GroupUnresolved);
    }

    /// <summary>Creates an invalid-address control-flow exception.</summary>
    private static ResolutionFailureException InvalidAddress()
    {
        return new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidAddress);
    }
}
