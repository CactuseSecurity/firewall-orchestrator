using System.Collections.ObjectModel;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>
/// Base value carried between Check Point positioning blocks.
/// </summary>
public abstract class CheckPointBlockValue
{
    /// <summary>Restricts construction to supported Check Point block values.</summary>
    private protected CheckPointBlockValue()
    { }
}

/// <summary>
/// A layer or section resolved from the caller's snapshot.
/// </summary>
public sealed class CheckPointScopeValue : CheckPointBlockValue
{
    /// <summary>Gets the selected layer node.</summary>
    public RulebaseGraphNode Layer { get; }
    /// <summary>Gets the optional selected section node.</summary>
    public RulebaseGraphNode? Section { get; }
    /// <summary>Gets the interpreted layer used for reference-safe validation.</summary>
    internal CheckPointLayerStructure? LayerStructure { get; }
    /// <summary>Gets the interpreted section used for reference-safe validation.</summary>
    internal CheckPointSectionStructure? SectionStructure { get; }

    /// <summary>Creates a scope from interpreted graph objects.</summary>
    internal CheckPointScopeValue(
        CheckPointLayerStructure layer,
        CheckPointSectionStructure? section = null)
    {
        LayerStructure = layer;
        SectionStructure = section;
        Layer = layer.Node;
        Section = section?.Node;
    }

    /// <summary>Creates a scope from normalized nodes.</summary>
    internal CheckPointScopeValue(
        RulebaseGraphNode layer,
        RulebaseGraphNode? section = null)
    {
        Layer = layer;
        Section = section;
    }
}

/// <summary>
/// Final semantic position produced by a Check Point recipe.
/// </summary>
public sealed class CheckPointPositionValue : CheckPointBlockValue
{
    /// <summary>Gets the calculated semantic rule position.</summary>
    public CheckPointRulePosition Position { get; }

    /// <summary>Creates a final block value from a semantic position.</summary>
    internal CheckPointPositionValue(CheckPointRulePosition position)
    {
        Position = position;
    }
}

/// <summary>
/// Supported ways to identify a Check Point access layer.
/// </summary>
public enum CheckPointLayerSelectionMode
{
    /// <summary>Selects the graph's unique initial layer.</summary>
    InitialLayer,
    /// <summary>Selects a layer by exact imported name.</summary>
    ExactName,
    /// <summary>Selects a layer by vendor identifier.</summary>
    Uid
}

/// <summary>
/// Immutable selector that identifies a layer by exactly one mode.
/// </summary>
public sealed class CheckPointLayerSelector
{
    /// <summary>Gets the layer selection mode.</summary>
    public CheckPointLayerSelectionMode Mode { get; }
    /// <summary>Gets the optional selector value.</summary>
    public string? Value { get; }

    /// <summary>Creates a validated immutable layer selector.</summary>
    private CheckPointLayerSelector(CheckPointLayerSelectionMode mode, string? value)
    {
        Mode = mode;
        Value = value;
    }

    /// <summary>Selects the unique initial layer.</summary>
    public static CheckPointLayerSelector InitialLayer() =>
        new(CheckPointLayerSelectionMode.InitialLayer, null);

    /// <summary>Selects a layer by its exact imported name.</summary>
    public static CheckPointLayerSelector ByExactName(string name) =>
        new(CheckPointLayerSelectionMode.ExactName, RequireValue(name, nameof(name)));

    /// <summary>Selects a layer by its vendor identifier.</summary>
    public static CheckPointLayerSelector ByUid(string uid) =>
        new(CheckPointLayerSelectionMode.Uid, RequireValue(uid, nameof(uid)));

    /// <summary>
    /// Rejects an absent selector value without changing ordinal matching semantics.
    /// </summary>
    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A selector value is required.", parameterName);
        }
        return value;
    }
}

/// <summary>
/// Supported ways to identify a section inside a selected layer.
/// </summary>
public enum CheckPointSectionSelectionMode
{
    /// <summary>Selects a section by exact imported name.</summary>
    ExactName,
    /// <summary>Selects a section by vendor identifier.</summary>
    Uid,
    /// <summary>Selects a section by normalized canonical name.</summary>
    NormalizedName
}

/// <summary>
/// Immutable selector that identifies a section by exactly one mode.
/// </summary>
public sealed class CheckPointSectionSelector
{
    /// <summary>Gets the section selection mode.</summary>
    public CheckPointSectionSelectionMode Mode { get; }
    /// <summary>Gets the validated selector value.</summary>
    public string Value { get; }

    /// <summary>Creates a validated immutable section selector.</summary>
    private CheckPointSectionSelector(CheckPointSectionSelectionMode mode, string value)
    {
        Mode = mode;
        Value = value;
    }

    /// <summary>Selects a section by its exact imported name.</summary>
    public static CheckPointSectionSelector ByExactName(string name) =>
        new(CheckPointSectionSelectionMode.ExactName, RequireValue(name, nameof(name)));

    /// <summary>Selects a section by its vendor identifier.</summary>
    public static CheckPointSectionSelector ByUid(string uid) =>
        new(CheckPointSectionSelectionMode.Uid, RequireValue(uid, nameof(uid)));

    /// <summary>Selects a section by its normalized canonical name.</summary>
    public static CheckPointSectionSelector ByNormalizedName(string name) =>
        new(CheckPointSectionSelectionMode.NormalizedName,
            RequireValue(name, nameof(name)).Trim().ToUpperInvariant());

    /// <summary>
    /// Rejects an absent selector value without changing ordinal matching semantics.
    /// </summary>
    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A selector value is required.", parameterName);
        }
        return value;
    }
}

/// <summary>Selects exactly one guarded inline layer by three-state address containment.</summary>
public sealed class CheckPointLayerGuardScopeBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
{
    /// <summary>Stable serialized kind for guarded-layer selection.</summary>
    public const string kKind = "check-point.layer-guard-scope";
    private static readonly IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> kNoChildren =
        new List<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>().AsReadOnly();
    private static readonly IReadOnlyList<PositioningBlockRole> kNoChildRoles =
        new List<PositioningBlockRole>().AsReadOnly();

    /// <inheritdoc />
    public string Kind => kKind;
    /// <inheritdoc />
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    /// <inheritdoc />
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => kNoChildren;
    /// <inheritdoc />
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kNoChildRoles;

    /// <summary>Validates the paired snapshots and selects one definite guarded inline layer.</summary>
    public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
        CheckPointPositioningContext context,
        IReadOnlyList<CheckPointBlockValue> childValues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PositioningBlockFailure<CheckPointBlockValue>? contextFailure =
            CheckPointBlockValidation.ValidateContext(context, Kind, out CheckPointGraphStructure? structure);
        if (contextFailure != null)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(contextFailure);
        }
        if (childValues.Count != 0)
        {
            return Result(PositioningBlockFailureCategory.InvalidRecipe);
        }
        CheckPointLayerGuardZonePairPositioningInput? positioningInput = context.LayerGuardZonePairInput;
        if (positioningInput == null ||
            !TryValidateSnapshot(context, positioningInput, structure!, out List<GuardCandidate> candidates))
        {
            return Result(PositioningBlockFailureCategory.InvalidSnapshot);
        }

        GuardMatchSummary matches = EvaluateCandidates(positioningInput, candidates, cancellationToken);
        return ResolveMatches(matches);
    }

    /// <summary>Evaluates every guard candidate and counts definite and indeterminate results.</summary>
    private static GuardMatchSummary EvaluateCandidates(
        CheckPointLayerGuardZonePairPositioningInput positioningInput,
        List<GuardCandidate> candidates,
        CancellationToken cancellationToken)
    {
        int definiteMatches = 0;
        int indeterminateCandidates = 0;
        CheckPointLayerStructure? selectedLayer = null;
        foreach (GuardCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPointGuardContainmentResult source = CheckPointGuardContainmentEvaluator.Evaluate(
                candidate.Guard.Source, positioningInput.NetworkInput.SourceRanges, cancellationToken);
            CheckPointGuardContainmentResult destination = CheckPointGuardContainmentEvaluator.Evaluate(
                candidate.Guard.Destination, positioningInput.NetworkInput.DestinationRanges, cancellationToken);
            if (source == CheckPointGuardContainmentResult.Contained && destination == CheckPointGuardContainmentResult.Contained)
            {
                definiteMatches++;
                selectedLayer = candidate.Layer;
            }
            else if (source != CheckPointGuardContainmentResult.NotContained &&
                destination != CheckPointGuardContainmentResult.NotContained)
            {
                indeterminateCandidates++;
            }
        }
        return new GuardMatchSummary(definiteMatches, indeterminateCandidates, selectedLayer);
    }

    /// <summary>Maps aggregate guard containment to one selected scope or failure.</summary>
    private static Task<PositioningBlockResult<CheckPointBlockValue>> ResolveMatches(GuardMatchSummary matches)
    {
        if (matches.DefiniteMatches > 1)
        {
            return Result(PositioningBlockFailureCategory.ScopeAmbiguous);
        }
        if (matches.IndeterminateCandidates > 0)
        {
            return Result(PositioningBlockFailureCategory.ScopeIndeterminate);
        }
        if (matches.DefiniteMatches == 0 || matches.SelectedLayer == null)
        {
            return Result(PositioningBlockFailureCategory.ScopeNotFound);
        }
        return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            new PositioningBlockSuccess<CheckPointBlockValue>(new CheckPointScopeValue(matches.SelectedLayer)));
    }

    /// <summary>Requires a one-to-one mapping between graph inline links and guard rows.</summary>
    private static bool TryValidateSnapshot(
        CheckPointPositioningContext context,
        CheckPointLayerGuardZonePairPositioningInput positioningInput,
        CheckPointGraphStructure structure,
        out List<GuardCandidate> candidates)
    {
        candidates = [];
        CheckPointLayerGuardSnapshot guards = positioningInput.GuardSnapshot;
        if (!HasMatchingSnapshotIdentity(guards, context.Snapshot))
        {
            return false;
        }
        List<RulebaseGraphLink> inlineLinks = [.. context.Snapshot.Links.Where(link =>
            link.LinkType == RulebaseLinkTypes.Inline)];
        if (!HasOneToOneGuardLinks(inlineLinks, guards))
        {
            return false;
        }
        foreach (RulebaseGraphLink link in inlineLinks)
        {
            if (!TryCreateGuardCandidate(link, structure, guards, out GuardCandidate? candidate))
            {
                return false;
            }
            candidates.Add(candidate!);
        }
        return true;
    }

    /// <summary>Checks that guard and graph snapshots describe the same management and gateway.</summary>
    private static bool HasMatchingSnapshotIdentity(
        CheckPointLayerGuardSnapshot guards,
        RulebaseGraphSnapshot graph) =>
        guards.ManagementId == graph.ManagementId &&
        guards.GatewayId == graph.GatewayId &&
        guards.ImportId > 0;

    /// <summary>Checks that every inline target has exactly one source rule and one guard row.</summary>
    private static bool HasOneToOneGuardLinks(
        List<RulebaseGraphLink> inlineLinks,
        CheckPointLayerGuardSnapshot guards) =>
        inlineLinks.Count == guards.GuardsByTargetLayerId.Count &&
        inlineLinks.All(link => link.SourceRuleId.HasValue && link.SourceRulebaseId.HasValue) &&
        inlineLinks.GroupBy(link => link.SourceRuleId).All(group => group.Count() == 1) &&
        inlineLinks.GroupBy(link => link.TargetRulebaseId).All(group => group.Count() == 1);

    /// <summary>Maps one validated inline link to its non-initial target layer and guard.</summary>
    private static bool TryCreateGuardCandidate(
        RulebaseGraphLink link,
        CheckPointGraphStructure structure,
        CheckPointLayerGuardSnapshot guards,
        out GuardCandidate? candidate)
    {
        candidate = null;
        CheckPointLayerStructure? layer = structure.Layers.SingleOrDefault(item =>
            item.Node.Id == link.TargetRulebaseId);
        if (layer == null || ReferenceEquals(layer, structure.InitialLayer) ||
            !guards.GuardsByTargetLayerId.TryGetValue(link.TargetRulebaseId, out CheckPointLayerGuard? guard) ||
            guard.SourceRuleId != link.SourceRuleId || guard.SourceRulebaseId != link.SourceRulebaseId ||
            guard.TargetLayerId != link.TargetRulebaseId)
        {
            return false;
        }
        candidate = new GuardCandidate(layer, guard);
        return true;
    }

    /// <summary>Creates one typed failed result.</summary>
    private static Task<PositioningBlockResult<CheckPointBlockValue>> Result(
        PositioningBlockFailureCategory category) =>
        Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            CheckPointBlockValidation.Failure(category, kKind));

    /// <summary>Associates one interpreted inline layer with its validated guard.</summary>
    private sealed record GuardCandidate(CheckPointLayerStructure Layer, CheckPointLayerGuard Guard);

    /// <summary>Aggregate result of evaluating every guarded inline-layer candidate.</summary>
    private sealed record GuardMatchSummary(
        int DefiniteMatches,
        int IndeterminateCandidates,
        CheckPointLayerStructure? SelectedLayer);
}

/// <summary>
/// Resolves a Check Point layer from the immutable snapshot.
/// </summary>
public sealed class CheckPointLayerScopeBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
{
    /// <summary>Stable serialized kind for direct layer selection.</summary>
    public const string kKind = "check-point.layer-scope";
    private static readonly ReadOnlyCollection<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> kNoChildren =
        new List<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>().AsReadOnly();
    private static readonly ReadOnlyCollection<PositioningBlockRole> kNoChildRoles =
        new List<PositioningBlockRole>().AsReadOnly();

    /// <inheritdoc />
    public string Kind => kKind;
    /// <inheritdoc />
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    /// <inheritdoc />
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => kNoChildren;
    /// <inheritdoc />
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kNoChildRoles;
    /// <summary>Gets the configured layer selector.</summary>
    public CheckPointLayerSelector Selector { get; }

    /// <summary>Creates a block for one validated layer selector.</summary>
    public CheckPointLayerScopeBlock(CheckPointLayerSelector selector)
    {
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// Resolves exactly one layer with ordinal identity matching.
    /// </summary>
    public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
        CheckPointPositioningContext context,
        IReadOnlyList<CheckPointBlockValue> childValues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PositioningBlockFailure<CheckPointBlockValue>? contextFailure =
            CheckPointBlockValidation.ValidateContext(context, Kind, out CheckPointGraphStructure? structure);
        if (contextFailure != null)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(contextFailure);
        }
        if (childValues.Count != 0)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.InvalidRecipe, Kind));
        }

        List<CheckPointLayerStructure> matches = MatchingLayers(structure!).ToList();
        return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(matches.Count switch
        {
            0 => CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.ScopeNotFound, Kind),
            1 => new PositioningBlockSuccess<CheckPointBlockValue>(new CheckPointScopeValue(matches[0])),
            _ => CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.ScopeAmbiguous, Kind)
        });
    }

    /// <summary>
    /// Applies the selected mode without fallback to another identity.
    /// </summary>
    private IEnumerable<CheckPointLayerStructure> MatchingLayers(CheckPointGraphStructure structure)
    {
        return Selector.Mode switch
        {
            CheckPointLayerSelectionMode.InitialLayer => structure.Layers.Where(layer =>
                ReferenceEquals(layer, structure.InitialLayer)),
            CheckPointLayerSelectionMode.ExactName => structure.Layers.Where(layer =>
                string.Equals(layer.Node.Name, Selector.Value, StringComparison.Ordinal)),
            CheckPointLayerSelectionMode.Uid => structure.Layers.Where(layer =>
                string.Equals(layer.Node.Uid, Selector.Value, StringComparison.Ordinal)),
            _ => []
        };
    }
}

/// <summary>
/// Resolves a section inside exactly the layer returned by its child.
/// </summary>
public sealed class CheckPointSectionScopeBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
{
    /// <summary>Stable serialized kind for section selection.</summary>
    public const string kKind = "check-point.section-scope";
    private static readonly ReadOnlyCollection<PositioningBlockRole> kChildRoles =
        new List<PositioningBlockRole> { CheckPointBlockRoles.Scope }.AsReadOnly();
    private readonly IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> children;

    /// <inheritdoc />
    public string Kind => kKind;
    /// <inheritdoc />
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    /// <inheritdoc />
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => children;
    /// <inheritdoc />
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kChildRoles;
    /// <summary>Gets the configured section selector.</summary>
    public CheckPointSectionSelector Selector { get; }

    /// <summary>Creates a section selector beneath one layer block.</summary>
    public CheckPointSectionScopeBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> layerBlock,
        CheckPointSectionSelector selector)
    {
        ArgumentNullException.ThrowIfNull(layerBlock);
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        children = new List<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>
        {
            layerBlock
        }.AsReadOnly();
    }

    /// <summary>
    /// Resolves exactly one section and never falls back outside its parent layer.
    /// </summary>
    public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
        CheckPointPositioningContext context,
        IReadOnlyList<CheckPointBlockValue> childValues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PositioningBlockFailure<CheckPointBlockValue>? contextFailure =
            CheckPointBlockValidation.ValidateContext(context, Kind, out _);
        if (contextFailure != null)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(contextFailure);
        }
        if (childValues.Count != 1 || childValues[0] is not CheckPointScopeValue { Section: null } parent)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.InvalidRecipe, Kind));
        }

        if (parent.LayerStructure == null)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.InvalidSnapshot, Kind));
        }
        List<CheckPointSectionStructure> matches = parent.LayerStructure.Sections.Where(Matches).ToList();
        return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(matches.Count switch
        {
            0 => CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.ScopeNotFound, Kind),
            1 => new PositioningBlockSuccess<CheckPointBlockValue>(
                new CheckPointScopeValue(parent.LayerStructure, matches[0])),
            _ => CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.ScopeAmbiguous, Kind)
        });
    }

    /// <summary>
    /// Applies the one configured section selection mode using ordinal comparison.
    /// </summary>
    private bool Matches(CheckPointSectionStructure section)
    {
        return Selector.Mode switch
        {
            CheckPointSectionSelectionMode.ExactName =>
                string.Equals(section.Node.Name, Selector.Value, StringComparison.Ordinal),
            CheckPointSectionSelectionMode.Uid =>
                string.Equals(section.Node.Uid, Selector.Value, StringComparison.Ordinal),
            CheckPointSectionSelectionMode.NormalizedName =>
                string.Equals(section.Node.Name.Trim().ToUpperInvariant(), Selector.Value, StringComparison.Ordinal),
            _ => false
        };
    }
}

/// <summary>
/// Resolves the beginning boundary of a selected layer or section.
/// </summary>
public sealed class BeginningOfBlock : CheckPointBoundaryBlock
{
    /// <summary>Stable serialized kind for beginning-of-scope placement.</summary>
    public const string kKind = "check-point.beginning-of";
    /// <inheritdoc />
    public override string Kind => kKind;

    /// <summary>Creates a beginning boundary for one scope block.</summary>
    public BeginningOfBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scopeBlock)
        : base(scopeBlock)
    { }

    /// <inheritdoc />
    protected override CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        RulebaseGraphRule firstRule = ScopeRules(scope)[0];
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Above, firstRule.Uid, algorithm);
    }

    /// <inheritdoc />
    protected override CheckPointRulePosition ResolveEmptyLayer(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Top, null, algorithm);
    }
}

/// <summary>
/// Resolves the ending boundary of a selected layer or section.
/// </summary>
public sealed class EndOfBlock : CheckPointBoundaryBlock
{
    /// <summary>Stable serialized kind for end-of-scope placement.</summary>
    public const string kKind = "check-point.end-of";
    /// <inheritdoc />
    public override string Kind => kKind;

    /// <summary>Creates an ending boundary for one scope block.</summary>
    public EndOfBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scopeBlock)
        : base(scopeBlock)
    { }

    /// <inheritdoc />
    protected override CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        IReadOnlyList<RulebaseGraphRule> rules = ScopeRules(scope);
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Below, rules[^1].Uid, algorithm);
    }

    /// <inheritdoc />
    protected override CheckPointRulePosition ResolveEmptyLayer(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Bottom, null, algorithm);
    }
}

/// <summary>
/// Shared fail-fast boundary resolution for beginning and end blocks.
/// </summary>
public abstract class CheckPointBoundaryBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
{
    private static readonly ReadOnlyCollection<PositioningBlockRole> kChildRoles =
        new List<PositioningBlockRole> { CheckPointBlockRoles.Scope }.AsReadOnly();
    private readonly IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> children;

    /// <inheritdoc />
    public abstract string Kind { get; }
    /// <inheritdoc />
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Position;
    /// <inheritdoc />
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => children;
    /// <inheritdoc />
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kChildRoles;

    /// <summary>Creates a boundary block with exactly one scope child.</summary>
    protected CheckPointBoundaryBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scopeBlock)
    {
        ArgumentNullException.ThrowIfNull(scopeBlock);
        children = new List<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>
        {
            scopeBlock
        }.AsReadOnly();
    }

    /// <summary>
    /// Produces a semantic position from one validated scope.
    /// </summary>
    public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
        CheckPointPositioningContext context,
        IReadOnlyList<CheckPointBlockValue> childValues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PositioningBlockFailure<CheckPointBlockValue>? contextFailure =
            CheckPointBlockValidation.ValidateContext(context, Kind, out CheckPointGraphStructure? structure);
        if (contextFailure != null)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(contextFailure);
        }
        if (childValues.Count != 1 || childValues[0] is not CheckPointScopeValue scope ||
            !CheckPointBlockValidation.IsScopeInSnapshot(scope, structure!))
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                CheckPointBlockValidation.Failure(PositioningBlockFailureCategory.NoInsertionPoint, Kind));
        }

        ProvisioningPositioningAlgorithm algorithm =
            context.CalculationContext.EffectiveSettings.PositioningAlgorithm;
        CheckPointRulePosition position = ScopeRules(scope).Count > 0
            ? ResolvePopulatedScope(scope, algorithm)
            : ResolveEmptyScope(scope, algorithm);
        return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            new PositioningBlockSuccess<CheckPointBlockValue>(new CheckPointPositionValue(position)));
    }

    /// <summary>
    /// Returns the rules directly represented by the selected scope.
    /// </summary>
    protected static IReadOnlyList<RulebaseGraphRule> ScopeRules(CheckPointScopeValue scope)
    {
        return scope.SectionStructure?.Rules ?? scope.LayerStructure!.OrderedRules;
    }

    /// <summary>
    /// Creates a validated semantic position.
    /// </summary>
    protected static CheckPointRulePosition Position(
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        ProvisioningPositioningAlgorithm algorithm)
    {
        return new CheckPointRulePosition(layerUid, anchorKind, anchorRuleUid, algorithm);
    }

    /// <summary>Resolves a boundary for a scope containing direct rules.</summary>
    protected abstract CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm);

    /// <summary>Resolves a boundary for an empty layer.</summary>
    protected abstract CheckPointRulePosition ResolveEmptyLayer(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm);

    /// <summary>
    /// Resolves an empty layer or the canonical single gap of an empty section.
    /// </summary>
    private CheckPointRulePosition ResolveEmptyScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        if (scope.Section == null)
        {
            return ResolveEmptyLayer(scope, algorithm);
        }

        int sectionIndex = IndexOfSection(scope.LayerStructure!, scope.SectionStructure!);
        RulebaseGraphRule? nextRule = FindNextRule(scope.LayerStructure!.Items, sectionIndex);
        if (nextRule != null)
        {
            return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Above, nextRule.Uid, algorithm);
        }
        RulebaseGraphRule? previousRule = FindPreviousRule(scope.LayerStructure.Items, sectionIndex);
        return previousRule != null
            ? Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Below, previousRule.Uid, algorithm)
            : Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Top, null, algorithm);
    }

    /// <summary>
    /// Finds the exact section instance in its parent layer.
    /// </summary>
    private static int IndexOfSection(
        CheckPointLayerStructure layer,
        CheckPointSectionStructure section)
    {
        for (int index = 0; index < layer.Items.Count; index++)
        {
            if (ReferenceEquals(layer.Items[index], section))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the first rule after an empty section.
    /// </summary>
    private static RulebaseGraphRule? FindNextRule(
        IReadOnlyList<CheckPointLayerItemStructure> items,
        int sectionIndex)
    {
        for (int index = sectionIndex + 1; index < items.Count; index++)
        {
            if (items[index] is CheckPointRuleItemStructure rule)
            {
                return rule.Rule;
            }
            if (items[index] is CheckPointSectionStructure { Rules.Count: > 0 } section)
            {
                return section.Rules[0];
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the last rule before an empty section.
    /// </summary>
    private static RulebaseGraphRule? FindPreviousRule(
        IReadOnlyList<CheckPointLayerItemStructure> items,
        int sectionIndex)
    {
        for (int index = sectionIndex - 1; index >= 0; index--)
        {
            if (items[index] is CheckPointRuleItemStructure rule)
            {
                return rule.Rule;
            }
            if (items[index] is CheckPointSectionStructure { Rules.Count: > 0 } section)
            {
                return section.Rules[^1];
            }
        }
        return null;
    }
}

/// <summary>Shared role identifiers used by Check Point positioning blocks.</summary>
internal static class CheckPointBlockRoles
{
    /// <summary>Role produced by blocks selecting a layer or section scope.</summary>
    internal static readonly PositioningBlockRole Scope = new("check-point.scope");
    /// <summary>Role produced by blocks calculating a final position.</summary>
    internal static readonly PositioningBlockRole Position = new("check-point.position");
}

/// <summary>Shared validation and failure construction for Check Point blocks.</summary>
internal static class CheckPointBlockValidation
{
    /// <summary>
    /// Verifies snapshot identity and the Phase 1 calculation inputs used by every block.
    /// </summary>
    internal static PositioningBlockFailure<CheckPointBlockValue>? ValidateContext(
        CheckPointPositioningContext? context,
        string blockKind,
        out CheckPointGraphStructure? structure)
    {
        structure = null;
        if (context?.CalculationContext?.TargetGateway == null ||
            context.CalculationContext.TargetManagement == null ||
            context.CalculationContext.EffectiveSettings == null ||
            context.Snapshot == null ||
            context.CalculationContext.TargetGateway.Id != context.Snapshot.GatewayId ||
            context.CalculationContext.TargetManagement.Id != context.Snapshot.ManagementId ||
            !Enum.IsDefined(context.CalculationContext.EffectiveSettings.PositioningAlgorithm) ||
            context.CalculationContext.EffectiveSettings.PositioningAlgorithm == ProvisioningPositioningAlgorithm.Undefined)
        {
            return Failure(PositioningBlockFailureCategory.InvalidSnapshot, blockKind);
        }
        if (!CheckPointGraphStructure.TryCreate(context.Snapshot, out structure))
        {
            return Failure(PositioningBlockFailureCategory.InvalidSnapshot, blockKind);
        }
        return null;
    }

    /// <summary>
    /// Confirms a block value still identifies objects in the caller's immutable snapshot.
    /// </summary>
    internal static bool IsScopeInSnapshot(
        CheckPointScopeValue scope,
        CheckPointGraphStructure structure)
    {
        CheckPointLayerStructure? layer = structure.Layers.SingleOrDefault(candidate =>
            ReferenceEquals(candidate.Node, scope.Layer));
        if (layer == null || scope.LayerStructure == null ||
            !ReferenceEquals(scope.LayerStructure.Node, scope.Layer))
        {
            return false;
        }
        return scope.Section == null && scope.SectionStructure == null ||
            scope.SectionStructure != null && ReferenceEquals(scope.SectionStructure.Node, scope.Section) &&
            layer.Sections.Count(section => ReferenceEquals(section.Node, scope.Section)) == 1;
    }

    /// <summary>Creates one category-only block failure.</summary>
    internal static PositioningBlockFailure<CheckPointBlockValue> Failure(
        PositioningBlockFailureCategory category,
        string blockKind)
    {
        return new PositioningBlockFailure<CheckPointBlockValue>(category, blockKind);
    }
}
