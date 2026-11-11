using System.Net;
using System.Net.Sockets;
using System.Numerics;
using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.NetworkTopology;
using FWO.Services.RulePositioning.Blocks;
using NetTools;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Stateless bounded search for one production-validated LayerGuard candidate.</summary>
public sealed class CheckPointLayerGuardHappyPathFinder : ICheckPointLayerGuardHappyPathFinder
{
    private const int kMaximumRenderedZonePairs = 10_000;
    private const int kMaximumRepresentativeAddresses = 4_096;
    private const int kMaximumValidatedAddressPairs = 100_000;
    private readonly SearchLimits limits;

    /// <summary>Creates a finder with production safety bounds.</summary>
    public CheckPointLayerGuardHappyPathFinder()
        : this(new SearchLimits(
            kMaximumRenderedZonePairs,
            kMaximumRepresentativeAddresses,
            kMaximumValidatedAddressPairs))
    { }

    /// <summary>Creates a finder with reduced bounds for focused boundary tests.</summary>
    internal CheckPointLayerGuardHappyPathFinder(
        int maximumRenderedZonePairs,
        int maximumRepresentativeAddresses,
        int maximumValidatedAddressPairs)
        : this(new SearchLimits(
            maximumRenderedZonePairs,
            maximumRepresentativeAddresses,
            maximumValidatedAddressPairs))
    { }

    private CheckPointLayerGuardHappyPathFinder(SearchLimits limits)
    {
        this.limits = limits;
    }

    /// <inheritdoc />
    public async Task<CheckPointLayerGuardHappyPathSearchResult> FindAsync(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        IReadOnlyCollection<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CheckPointSectionNameTemplate sectionNameTemplate,
        CancellationToken cancellationToken)
    {
        ValidateInputs(context, graph, guards, networkZones, configuration, sectionNameTemplate);
        cancellationToken.ThrowIfCancellationRequested();
        SearchState state = new();
        List<NetworkZone> zones = networkZones.OrderBy(zone => zone.Id).ToList();
        if (!TryValidateSnapshots(graph, guards, zones, out CheckPointGraphStructure? structure))
        {
            return Result(CheckPointLayerGuardHappyPathSearchStatus.Indeterminate,
                CheckPointLayerGuardHappyPathSearchReason.InvalidSnapshot, state);
        }
        List<NetworkZone> searchZones = zones.Where(zone => zone.IPRanges.Length > 0).ToList();

        StructuralBuildResult structural = BuildStructuralMatches(
            structure!, guards, searchZones, sectionNameTemplate, state, cancellationToken);
        if (structural.LimitReached)
        {
            return Result(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached,
                CheckPointLayerGuardHappyPathSearchReason.RenderedZonePairLimit, state);
        }

        foreach (StructuralMatch match in structural.Matches)
        {
            CheckPointLayerGuardHappyPathSearchResult? result = await SearchMatchAsync(
                context, graph, guards, zones, configuration, match, state, cancellationToken);
            if (result != null)
            {
                return result;
            }
        }
        return Result(
            state.HasIndeterminateGuard ? CheckPointLayerGuardHappyPathSearchStatus.Indeterminate :
                CheckPointLayerGuardHappyPathSearchStatus.NoCandidate,
            state.HasIndeterminateGuard ? CheckPointLayerGuardHappyPathSearchReason.GuardIndeterminate :
                CheckPointLayerGuardHappyPathSearchReason.Exhausted,
            state);
    }

    /// <summary>Searches representative singleton pairs for one structural layer/section match.</summary>
    private async Task<CheckPointLayerGuardHappyPathSearchResult?> SearchMatchAsync(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        List<NetworkZone> zones,
        RulePositioningConfiguration configuration,
        StructuralMatch match,
        SearchState state,
        CancellationToken cancellationToken)
    {
        List<CheckPointGuardSide> sourceSides = guards.GuardsByTargetLayerId.Values
            .Select(guard => guard.Source).ToList();
        CheckPointRepresentativeAddressPartition sources = PartitionAddresses(
            match.SourceZone, zones, sourceSides, limits.MaximumRepresentativeAddresses, cancellationToken);
        if (sources.LimitReached)
        {
            return Result(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached,
                CheckPointLayerGuardHappyPathSearchReason.RepresentativeAddressLimit, state);
        }
        List<CheckPointGuardSide> destinationSides = guards.GuardsByTargetLayerId.Values
            .Select(guard => guard.Destination).ToList();
        CheckPointRepresentativeAddressPartition destinations = PartitionAddresses(
            match.DestinationZone, zones, destinationSides, limits.MaximumRepresentativeAddresses, cancellationToken);
        if (destinations.LimitReached)
        {
            return Result(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached,
                CheckPointLayerGuardHappyPathSearchReason.RepresentativeAddressLimit, state);
        }

        foreach (IPAddress source in sources.Addresses)
        {
            foreach (IPAddress destination in destinations.Addresses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (state.AddressPairValidationCount >= limits.MaximumValidatedAddressPairs)
                {
                    return Result(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached,
                        CheckPointLayerGuardHappyPathSearchReason.AddressPairLimit, state);
                }
                state.AddressPairValidationCount++;
                CheckPointLayerGuardHappyPathCandidate? candidate = await ValidatePairAsync(
                    context, graph, guards, zones, configuration, match, source, destination, state,
                    cancellationToken);
                if (candidate != null)
                {
                    return Result(CheckPointLayerGuardHappyPathSearchStatus.Found,
                        CheckPointLayerGuardHappyPathSearchReason.CandidateValidated, state, candidate);
                }
            }
        }
        return null;
    }

    /// <summary>Validates one representative pair with the production resolver, guards, and recipe.</summary>
    private static async Task<CheckPointLayerGuardHappyPathCandidate?> ValidatePairAsync(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        List<NetworkZone> zones,
        RulePositioningConfiguration configuration,
        StructuralMatch match,
        IPAddress source,
        IPAddress destination,
        SearchState state,
        CancellationToken cancellationToken)
    {
        if (!ResolvesUniquely(source, match.SourceZone, zones, configuration, cancellationToken) ||
            !ResolvesUniquely(destination, match.DestinationZone, zones, configuration, cancellationToken))
        {
            state.ZoneRejectionCount++;
            return null;
        }
        CreationRuleNetworkInput networkInput = new([Singleton(source)], [Singleton(destination)]);
        GuardValidation guardValidation = ValidateGuards(guards, networkInput, match.Guard, cancellationToken);
        if (!guardValidation.IsValid)
        {
            state.GuardRejectionCount++;
            state.HasIndeterminateGuard |= guardValidation.IsIndeterminate;
            return null;
        }

        CheckPointRulePosition? position = await ExecuteRecipeAsync(
            context, graph, guards, networkInput, match.RenderedSectionName, cancellationToken);
        if (position == null)
        {
            state.RecipeRejectionCount++;
            return null;
        }
        return Candidate(match, source, destination, position);
    }

    /// <summary>Builds deterministic zone-pair/layer combinations having one normalized section match.</summary>
    private StructuralBuildResult BuildStructuralMatches(
        CheckPointGraphStructure structure,
        CheckPointLayerGuardSnapshot guards,
        List<NetworkZone> zones,
        CheckPointSectionNameTemplate template,
        SearchState state,
        CancellationToken cancellationToken)
    {
        List<StructuralMatch> matches = [];
        foreach (CheckPointLayerGuard guard in guards.GuardsByTargetLayerId.Values.OrderBy(value => value.TargetLayerId))
        {
            CheckPointLayerStructure? layer = structure.Layers.SingleOrDefault(candidate => candidate.Node.Id == guard.TargetLayerId);
            if (layer == null)
            {
                return new StructuralBuildResult([], false);
            }
            foreach (NetworkZone source in zones)
            {
                foreach (NetworkZone destination in zones)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (state.RenderedZonePairCount >= limits.MaximumRenderedZonePairs)
                    {
                        return new StructuralBuildResult(matches, true);
                    }
                    state.RenderedZonePairCount++;
                    TryAddStructuralMatch(matches, layer, guard, source, destination, template, state);
                }
            }
        }
        return new StructuralBuildResult(matches
            .OrderBy(match => match.Layer.Node.Id)
            .ThenBy(match => match.Section.Node.Id)
            .ThenBy(match => match.SourceZone.Id)
            .ThenBy(match => match.DestinationZone.Id)
            .ToList(), false);
    }

    /// <summary>Adds one pair only when rendering succeeds and exactly one section matches.</summary>
    private static void TryAddStructuralMatch(
        List<StructuralMatch> matches,
        CheckPointLayerStructure layer,
        CheckPointLayerGuard guard,
        NetworkZone source,
        NetworkZone destination,
        CheckPointSectionNameTemplate template,
        SearchState state)
    {
        Dictionary<CheckPointSectionNameVariable, string> values = new()
        {
            [CheckPointSectionNameVariable.SourceZone] = source.Name,
            [CheckPointSectionNameVariable.DestinationZone] = destination.Name
        };
        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> rendered = template.Render(values);
        if (!rendered.TryGetValue(out string? name))
        {
            state.SectionRejectionCount++;
            return;
        }
        List<CheckPointSectionStructure> sections = layer.Sections.Where(section =>
            string.Equals(section.Node.Name.Trim().ToUpperInvariant(), name, StringComparison.Ordinal)).ToList();
        if (sections.Count != 1)
        {
            state.SectionRejectionCount++;
            return;
        }
        state.StructuralMatchCount++;
        matches.Add(new StructuralMatch(layer, sections[0], guard, source, destination, name));
    }

    /// <summary>Partitions one target zone wherever zone or any guard membership can change.</summary>
    internal static CheckPointRepresentativeAddressPartition PartitionAddresses(
        NetworkZone target,
        IReadOnlyCollection<NetworkZone> zones,
        IReadOnlyCollection<CheckPointGuardSide> guardSides,
        int maximumAddresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(guardSides);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAddresses);
        Dictionary<AddressFamily, List<BigInteger>> boundaries = BuildBoundaryIndex(zones, guardSides);
        List<IPAddress> representatives = [];
        HashSet<IPAddress> seen = [];
        int targetRangeCount = 0;
        foreach (IPAddressRange targetRange in target.IPRanges
            .OrderBy(range => AddressFamilyOrder(range.Begin))
            .ThenBy(range => IpOperations.ToBigInteger(range.Begin)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetRangeCount++;
            if (!TryAddRepresentative(targetRange.Begin, seen, representatives, maximumAddresses))
            {
                return new CheckPointRepresentativeAddressPartition(representatives, true, targetRangeCount);
            }
            List<BigInteger> familyBoundaries = boundaries.GetValueOrDefault(targetRange.Begin.AddressFamily) ?? [];
            int index = FirstBoundaryAfter(familyBoundaries, IpOperations.ToBigInteger(targetRange.Begin));
            BigInteger targetEnd = IpOperations.ToBigInteger(targetRange.End);
            while (index < familyBoundaries.Count && familyBoundaries[index] <= targetEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IPAddress representative = IpOperations.FromBigInteger(
                    familyBoundaries[index], targetRange.Begin.AddressFamily);
                if (!TryAddRepresentative(representative, seen, representatives, maximumAddresses))
                {
                    return new CheckPointRepresentativeAddressPartition(representatives, true, targetRangeCount);
                }
                index++;
            }
        }
        return new CheckPointRepresentativeAddressPartition(representatives, false, targetRangeCount);
    }

    /// <summary>Builds one sorted membership-change index per address family.</summary>
    private static Dictionary<AddressFamily, List<BigInteger>> BuildBoundaryIndex(
        IReadOnlyCollection<NetworkZone> zones,
        IReadOnlyCollection<CheckPointGuardSide> guardSides)
    {
        IEnumerable<IPAddressRange> ranges = zones.SelectMany(zone => zone.IPRanges)
            .Concat(guardSides.SelectMany(side => side.Terms)
                .SelectMany(term => term.IncludedRanges.Concat(term.ExcludedRanges)));
        return ranges.GroupBy(range => range.Begin.AddressFamily).ToDictionary(
            group => group.Key,
            group => group.SelectMany(range => new[]
                {
                    IpOperations.ToBigInteger(range.Begin),
                    IpOperations.ToBigInteger(range.End) + BigInteger.One
                })
                .Distinct()
                .OrderBy(value => value)
                .ToList());
    }

    /// <summary>Returns the first sorted boundary strictly greater than the supplied address.</summary>
    private static int FirstBoundaryAfter(List<BigInteger> boundaries, BigInteger address)
    {
        int low = 0;
        int high = boundaries.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (boundaries[middle] <= address) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    /// <summary>Adds one unique representative while enforcing the caller's limit immediately.</summary>
    private static bool TryAddRepresentative(
        IPAddress address,
        HashSet<IPAddress> seen,
        List<IPAddress> representatives,
        int maximumAddresses)
    {
        if (!seen.Add(address)) return true;
        if (representatives.Count >= maximumAddresses) return false;
        representatives.Add(address);
        return true;
    }

    /// <summary>Requires complete singleton resolution to exactly the intended persisted zone.</summary>
    private static bool ResolvesUniquely(
        IPAddress address,
        NetworkZone intendedZone,
        List<NetworkZone> zones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        List<NetworkZone> resolved = NetworkZoneResolver.ResolveZones(
            [Singleton(address)], zones, configuration.AutoCalculateInternetZone,
            configuration.InternetLocalZoneName, cancellationToken, out List<IPAddressRange> unassignable);
        return unassignable.Count == 0 && resolved.Count == 1 && resolved[0].Id == intendedZone.Id;
    }

    /// <summary>Applies the production three-state semantics to every guard.</summary>
    private static GuardValidation ValidateGuards(
        CheckPointLayerGuardSnapshot guards,
        CreationRuleNetworkInput input,
        CheckPointLayerGuard expected,
        CancellationToken cancellationToken)
    {
        int definite = 0;
        int indeterminate = 0;
        long selectedTarget = 0;
        foreach (CheckPointLayerGuard guard in guards.GuardsByTargetLayerId.Values.OrderBy(value => value.TargetLayerId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPointGuardContainmentResult source = CheckPointGuardContainmentEvaluator.Evaluate(
                guard.Source, input.SourceRanges, cancellationToken);
            CheckPointGuardContainmentResult destination = CheckPointGuardContainmentEvaluator.Evaluate(
                guard.Destination, input.DestinationRanges, cancellationToken);
            if (source == CheckPointGuardContainmentResult.Contained &&
                destination == CheckPointGuardContainmentResult.Contained)
            {
                definite++;
                selectedTarget = guard.TargetLayerId;
            }
            else if (source != CheckPointGuardContainmentResult.NotContained &&
                destination != CheckPointGuardContainmentResult.NotContained)
            {
                indeterminate++;
            }
        }
        return new GuardValidation(
            definite == 1 && indeterminate == 0 && selectedTarget == expected.TargetLayerId,
            indeterminate > 0);
    }

    /// <summary>Executes the same block composition used by LayerGuard production positioning.</summary>
    private static async Task<CheckPointRulePosition?> ExecuteRecipeAsync(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        CreationRuleNetworkInput networkInput,
        string normalizedSectionName,
        CancellationToken cancellationToken)
    {
        CheckPointLayerGuardScopeBlock layer = new();
        CheckPointSectionScopeBlock section = new(layer,
            CheckPointSectionSelector.ByNormalizedName(normalizedSectionName));
        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = new(new EndOfBlock(section));
        CheckPointPositioningContext positioningContext = new(
            context, graph, new CheckPointLayerGuardZonePairPositioningInput(networkInput, guards));
        PositioningBlockResult<CheckPointBlockValue> result = await new PositioningRecipeExecutor<
            CheckPointPositioningContext, CheckPointBlockValue>().ExecuteAsync(
                recipe, positioningContext, cancellationToken).ConfigureAwait(false);
        return result is PositioningBlockSuccess<CheckPointBlockValue>
        { Value: CheckPointPositionValue positionValue } &&
            positionValue.Position.PositioningAlgorithm ==
                ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection
            ? positionValue.Position
            : null;
    }

    /// <summary>Creates the bounded public candidate from one validated internal match.</summary>
    private static CheckPointLayerGuardHappyPathCandidate Candidate(
        StructuralMatch match,
        IPAddress source,
        IPAddress destination,
        CheckPointRulePosition position) => new(
            source,
            destination,
            match.SourceZone.Id,
            match.SourceZone.Name,
            match.DestinationZone.Id,
            match.DestinationZone.Name,
            match.Guard.SourceRuleId,
            match.Layer.Node.Id,
            match.Layer.Node.Name,
            match.Layer.Node.Uid,
            match.Section.Node.Id,
            match.Section.Node.Name,
            match.Section.Node.Uid,
            match.RenderedSectionName,
            position);

    /// <summary>Builds one terminal immutable result from per-call mutable counters.</summary>
    private static CheckPointLayerGuardHappyPathSearchResult Result(
        CheckPointLayerGuardHappyPathSearchStatus status,
        CheckPointLayerGuardHappyPathSearchReason reason,
        SearchState state,
        CheckPointLayerGuardHappyPathCandidate? candidate = null) => new(
            status,
            reason,
            candidate,
            new CheckPointLayerGuardHappyPathSearchStatistics(
                state.RenderedZonePairCount,
                state.StructuralMatchCount,
                state.AddressPairValidationCount,
                state.ZoneRejectionCount,
                state.GuardRejectionCount,
                state.SectionRejectionCount,
                state.RecipeRejectionCount));

    /// <summary>Validates public arguments before any search work begins.</summary>
    private static void ValidateInputs(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        IReadOnlyCollection<NetworkZone> zones,
        RulePositioningConfiguration configuration,
        CheckPointSectionNameTemplate template)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(guards);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(template);
    }

    /// <summary>Rejects mismatched snapshots and malformed persisted zones.</summary>
    private static bool TryValidateSnapshots(
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        IReadOnlyCollection<NetworkZone> zones,
        out CheckPointGraphStructure? structure) =>
        CheckPointGraphStructure.TryCreate(graph, out structure) &&
        graph.ManagementId == guards.ManagementId && graph.GatewayId == guards.GatewayId &&
        zones.Count > 0 && zones.All(zone => zone != null && zone.Id > 0 &&
            !string.IsNullOrWhiteSpace(zone.Name) && zone.IPRanges != null) &&
        zones.Select(zone => zone.Id).Distinct().Count() == zones.Count;

    private static IPAddressRange Singleton(IPAddress address) => new(address, address);

    private static int AddressFamilyOrder(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1;

    private sealed record StructuralMatch(
        CheckPointLayerStructure Layer,
        CheckPointSectionStructure Section,
        CheckPointLayerGuard Guard,
        NetworkZone SourceZone,
        NetworkZone DestinationZone,
        string RenderedSectionName);

    private sealed record StructuralBuildResult(List<StructuralMatch> Matches, bool LimitReached);
    private sealed record GuardValidation(bool IsValid, bool IsIndeterminate);

    private sealed record SearchLimits
    {
        internal int MaximumRenderedZonePairs { get; }
        internal int MaximumRepresentativeAddresses { get; }
        internal int MaximumValidatedAddressPairs { get; }

        internal SearchLimits(int renderedZonePairs, int representativeAddresses, int validatedAddressPairs)
        {
            if (renderedZonePairs <= 0 || representativeAddresses <= 0 || validatedAddressPairs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(renderedZonePairs));
            }
            MaximumRenderedZonePairs = renderedZonePairs;
            MaximumRepresentativeAddresses = representativeAddresses;
            MaximumValidatedAddressPairs = validatedAddressPairs;
        }
    }

    private sealed class SearchState
    {
        internal int RenderedZonePairCount;
        internal int StructuralMatchCount;
        internal int AddressPairValidationCount;
        internal int ZoneRejectionCount;
        internal int GuardRejectionCount;
        internal int SectionRejectionCount;
        internal int RecipeRejectionCount;
        internal bool HasIndeterminateGuard;
    }
}

/// <summary>Bounded representative addresses and the amount of target-zone work performed.</summary>
internal sealed record CheckPointRepresentativeAddressPartition(
    IReadOnlyList<IPAddress> Addresses,
    bool LimitReached,
    int TargetRangeCount);
