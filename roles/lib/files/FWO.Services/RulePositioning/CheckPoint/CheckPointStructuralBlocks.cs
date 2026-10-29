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
    private protected CheckPointBlockValue()
    { }
}

/// <summary>
/// A layer or section resolved from the caller's snapshot.
/// </summary>
public sealed class CheckPointScopeValue : CheckPointBlockValue
{
    public RulebaseGraphNode Layer { get; }
    public RulebaseGraphNode? Section { get; }
    internal CheckPointLayerStructure? LayerStructure { get; }
    internal CheckPointSectionStructure? SectionStructure { get; }

    internal CheckPointScopeValue(
        CheckPointLayerStructure layer,
        CheckPointSectionStructure? section = null)
    {
        LayerStructure = layer;
        SectionStructure = section;
        Layer = layer.Node;
        Section = section?.Node;
    }

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
    public CheckPointRulePosition Position { get; }

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
    InitialLayer,
    ExactName,
    Uid
}

/// <summary>
/// Immutable selector that identifies a layer by exactly one mode.
/// </summary>
public sealed class CheckPointLayerSelector
{
    public CheckPointLayerSelectionMode Mode { get; }
    public string? Value { get; }

    private CheckPointLayerSelector(CheckPointLayerSelectionMode mode, string? value)
    {
        Mode = mode;
        Value = value;
    }

    public static CheckPointLayerSelector InitialLayer() =>
        new(CheckPointLayerSelectionMode.InitialLayer, null);

    public static CheckPointLayerSelector ByExactName(string name) =>
        new(CheckPointLayerSelectionMode.ExactName, RequireValue(name, nameof(name)));

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
    ExactName,
    Uid,
    NormalizedName
}

/// <summary>
/// Immutable selector that identifies a section by exactly one mode.
/// </summary>
public sealed class CheckPointSectionSelector
{
    public CheckPointSectionSelectionMode Mode { get; }
    public string Value { get; }

    private CheckPointSectionSelector(CheckPointSectionSelectionMode mode, string value)
    {
        Mode = mode;
        Value = value;
    }

    public static CheckPointSectionSelector ByExactName(string name) =>
        new(CheckPointSectionSelectionMode.ExactName, RequireValue(name, nameof(name)));

    public static CheckPointSectionSelector ByUid(string uid) =>
        new(CheckPointSectionSelectionMode.Uid, RequireValue(uid, nameof(uid)));

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
    public const string kKind = "check-point.layer-guard-scope";
    private static readonly IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> kNoChildren =
        new ReadOnlyCollection<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>([]);
    private static readonly IReadOnlyList<PositioningBlockRole> kNoChildRoles =
        new ReadOnlyCollection<PositioningBlockRole>([]);

    public string Kind => kKind;
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => kNoChildren;
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
        if (context.KfwInput == null || !TryValidateSnapshot(context, structure!, out List<GuardCandidate> candidates))
        {
            return Result(PositioningBlockFailureCategory.InvalidSnapshot);
        }

        int definiteMatches = 0;
        int indeterminateCandidates = 0;
        CheckPointLayerStructure? selectedLayer = null;
        foreach (GuardCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPointGuardContainmentResult source = CheckPointGuardContainmentEvaluator.Evaluate(
                candidate.Guard.Source, context.KfwInput.NetworkInput.SourceRanges, cancellationToken);
            CheckPointGuardContainmentResult destination = CheckPointGuardContainmentEvaluator.Evaluate(
                candidate.Guard.Destination, context.KfwInput.NetworkInput.DestinationRanges, cancellationToken);
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

        if (definiteMatches > 1)
        {
            return Result(PositioningBlockFailureCategory.ScopeAmbiguous);
        }
        if (indeterminateCandidates > 0)
        {
            return Result(PositioningBlockFailureCategory.ScopeIndeterminate);
        }
        if (definiteMatches == 0 || selectedLayer == null)
        {
            return Result(PositioningBlockFailureCategory.ScopeNotFound);
        }
        return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            new PositioningBlockSuccess<CheckPointBlockValue>(new CheckPointScopeValue(selectedLayer)));
    }

    /// <summary>Requires a one-to-one mapping between graph inline links and guard rows.</summary>
    private static bool TryValidateSnapshot(
        CheckPointPositioningContext context,
        CheckPointGraphStructure structure,
        out List<GuardCandidate> candidates)
    {
        candidates = [];
        CheckPointLayerGuardSnapshot guards = context.KfwInput!.GuardSnapshot;
        if (guards.ManagementId != context.Snapshot.ManagementId ||
            guards.GatewayId != context.Snapshot.GatewayId || guards.ImportId <= 0)
        {
            return false;
        }
        List<RulebaseGraphLink> inlineLinks = [.. context.Snapshot.Links.Where(link =>
            link.LinkType == RulebaseLinkTypes.Inline)];
        if (inlineLinks.Count != guards.GuardsByTargetLayerId.Count ||
            inlineLinks.Any(link => !link.SourceRuleId.HasValue || !link.SourceRulebaseId.HasValue) ||
            inlineLinks.GroupBy(link => link.SourceRuleId).Any(group => group.Count() != 1) ||
            inlineLinks.GroupBy(link => link.TargetRulebaseId).Any(group => group.Count() != 1))
        {
            return false;
        }
        foreach (RulebaseGraphLink link in inlineLinks)
        {
            CheckPointLayerStructure? layer = structure.Layers.SingleOrDefault(candidate =>
                candidate.Node.Id == link.TargetRulebaseId);
            if (layer == null || ReferenceEquals(layer, structure.InitialLayer) ||
                !guards.GuardsByTargetLayerId.TryGetValue(link.TargetRulebaseId, out CheckPointLayerGuard? guard) ||
                guard.SourceRuleId != link.SourceRuleId || guard.SourceRulebaseId != link.SourceRulebaseId ||
                guard.TargetLayerId != link.TargetRulebaseId)
            {
                return false;
            }
            candidates.Add(new GuardCandidate(layer, guard));
        }
        return true;
    }

    /// <summary>Creates one typed failed result.</summary>
    private static Task<PositioningBlockResult<CheckPointBlockValue>> Result(
        PositioningBlockFailureCategory category) =>
        Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            CheckPointBlockValidation.Failure(category, kKind));

    private sealed record GuardCandidate(CheckPointLayerStructure Layer, CheckPointLayerGuard Guard);
}

/// <summary>
/// Resolves a Check Point layer from the immutable snapshot.
/// </summary>
public sealed class CheckPointLayerScopeBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
{
    public const string kKind = "check-point.layer-scope";
    private static readonly ReadOnlyCollection<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> kNoChildren =
        new List<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>().AsReadOnly();
    private static readonly ReadOnlyCollection<PositioningBlockRole> kNoChildRoles =
        new List<PositioningBlockRole>().AsReadOnly();

    public string Kind => kKind;
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => kNoChildren;
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kNoChildRoles;
    public CheckPointLayerSelector Selector { get; }

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
    public const string kKind = "check-point.section-scope";
    private static readonly ReadOnlyCollection<PositioningBlockRole> kChildRoles =
        new List<PositioningBlockRole> { CheckPointBlockRoles.Scope }.AsReadOnly();
    private readonly IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> children;

    public string Kind => kKind;
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Scope;
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => children;
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kChildRoles;
    public CheckPointSectionSelector Selector { get; }

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
    public const string kKind = "check-point.beginning-of";
    public override string Kind => kKind;

    public BeginningOfBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scopeBlock)
        : base(scopeBlock)
    { }

    protected override CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        RulebaseGraphRule firstRule = ScopeRules(scope)[0];
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Above, firstRule.Uid, algorithm);
    }

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
    public const string kKind = "check-point.end-of";
    public override string Kind => kKind;

    public EndOfBlock(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scopeBlock)
        : base(scopeBlock)
    { }

    protected override CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm)
    {
        IReadOnlyList<RulebaseGraphRule> rules = ScopeRules(scope);
        return Position(scope.Layer.Uid, CheckPointPositionAnchorKind.Below, rules[^1].Uid, algorithm);
    }

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

    public abstract string Kind { get; }
    public PositioningBlockRole OutputRole => CheckPointBlockRoles.Position;
    public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => children;
    public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => kChildRoles;

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

    protected abstract CheckPointRulePosition ResolvePopulatedScope(
        CheckPointScopeValue scope,
        ProvisioningPositioningAlgorithm algorithm);

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

internal static class CheckPointBlockRoles
{
    internal static readonly PositioningBlockRole Scope = new("check-point.scope");
    internal static readonly PositioningBlockRole Position = new("check-point.position");
}

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

    internal static PositioningBlockFailure<CheckPointBlockValue> Failure(
        PositioningBlockFailureCategory category,
        string blockKind)
    {
        return new PositioningBlockFailure<CheckPointBlockValue>(category, blockKind);
    }
}
