using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointStructuralBlocksTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection;

    [TestCase(CheckPointLayerSelectionMode.InitialLayer, null, "layer-initial")]
    [TestCase(CheckPointLayerSelectionMode.ExactName, "Inline", "layer-inline")]
    [TestCase(CheckPointLayerSelectionMode.Uid, "layer-inline", "layer-inline")]
    public async Task LayerScope_SelectsByConfiguredMode(
        CheckPointLayerSelectionMode mode,
        string? value,
        string expectedUid)
    {
        CheckPointLayerSelector selector = mode switch
        {
            CheckPointLayerSelectionMode.InitialLayer => CheckPointLayerSelector.InitialLayer(),
            CheckPointLayerSelectionMode.ExactName => CheckPointLayerSelector.ByExactName(value!),
            _ => CheckPointLayerSelector.ByUid(value!)
        };

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(
            new CheckPointLayerScopeBlock(selector),
            StandardContext());

        Assert.That(SuccessScope(result).Layer.Uid, Is.EqualTo(expectedUid));
    }

    [Test]
    public async Task LayerName_MatchesExactlyWithOrdinalComparison()
    {
        CheckPointPositioningContext context = StandardContext();

        PositioningBlockResult<CheckPointBlockValue> wrongCase = await Execute(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByExactName("network")),
            context);
        PositioningBlockResult<CheckPointBlockValue> exact = await Execute(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByExactName("Network")),
            context);

        AssertFailure(wrongCase, PositioningBlockFailureCategory.ScopeNotFound, CheckPointLayerScopeBlock.kKind);
        Assert.That(SuccessScope(exact).Layer.Uid, Is.EqualTo("layer-initial"));
    }

    [Test]
    public async Task LayerScope_ReturnsMissingAndAmbiguousFailures()
    {
        Rulebase first = StructuralRulebase(1, "layer-1", "Duplicate");
        Rulebase second = StructuralRulebase(2, "layer-2", "Duplicate");
        CheckPointPositioningContext context = Context(Snapshot(
            [first, second],
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                Link(1, null, 2, RulebaseLinkTypes.Ordered)
            ]));

        PositioningBlockResult<CheckPointBlockValue> missing = await Execute(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByUid("missing")),
            context);
        PositioningBlockResult<CheckPointBlockValue> ambiguous = await Execute(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByExactName("Duplicate")),
            context);

        AssertFailure(missing, PositioningBlockFailureCategory.ScopeNotFound, CheckPointLayerScopeBlock.kKind);
        AssertFailure(ambiguous, PositioningBlockFailureCategory.ScopeAmbiguous, CheckPointLayerScopeBlock.kKind);
    }

    [TestCase(CheckPointSectionSelectionMode.ExactName, "Applications", "section-app")]
    [TestCase(CheckPointSectionSelectionMode.Uid, "section-app", "section-app")]
    public async Task SectionScope_SelectsWithinParentLayer(
        CheckPointSectionSelectionMode mode,
        string value,
        string expectedUid)
    {
        CheckPointSectionSelector selector = mode == CheckPointSectionSelectionMode.ExactName
            ? CheckPointSectionSelector.ByExactName(value)
            : CheckPointSectionSelector.ByUid(value);
        CheckPointSectionScopeBlock block = new(InitialLayer(), selector);

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(block, StandardContext());

        Assert.That(SuccessScope(result).Section!.Uid, Is.EqualTo(expectedUid));
    }

    [Test]
    public async Task SectionScope_DoesNotFallBackToAnotherLayer()
    {
        CheckPointSectionScopeBlock block = new(
            InitialLayer(),
            CheckPointSectionSelector.ByUid("inline-section"));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(block, StandardContext());

        AssertFailure(result, PositioningBlockFailureCategory.ScopeNotFound, CheckPointSectionScopeBlock.kKind);
    }

    [Test]
    public async Task SectionName_ReturnsAmbiguousFailureAndUsesOrdinalMatching()
    {
        Rulebase layer = StructuralRulebase(1, "layer-1", "Network");
        Rulebase first = StructuralRulebase(2, "section-1", "Duplicate");
        Rulebase second = StructuralRulebase(3, "section-2", "Duplicate");
        CheckPointPositioningContext context = Context(Snapshot(
            [layer, first, second],
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                SectionLink(1, 2),
                SectionLink(2, 3)
            ]));

        PositioningBlockResult<CheckPointBlockValue> ambiguous = await Execute(
            new CheckPointSectionScopeBlock(
                InitialLayer(),
                CheckPointSectionSelector.ByExactName("Duplicate")),
            context);
        PositioningBlockResult<CheckPointBlockValue> wrongCase = await Execute(
            new CheckPointSectionScopeBlock(
                InitialLayer(),
                CheckPointSectionSelector.ByExactName("duplicate")),
            context);

        AssertFailure(ambiguous, PositioningBlockFailureCategory.ScopeAmbiguous, CheckPointSectionScopeBlock.kKind);
        AssertFailure(wrongCase, PositioningBlockFailureCategory.ScopeNotFound, CheckPointSectionScopeBlock.kKind);
    }

    [Test]
    public async Task ParentFailure_PropagatesWithoutEvaluatingSection()
    {
        CheckPointSectionScopeBlock block = new(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByUid("missing")),
            CheckPointSectionSelector.ByUid("section-app"));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(block, StandardContext());

        AssertFailure(result, PositioningBlockFailureCategory.ScopeNotFound, CheckPointLayerScopeBlock.kKind);
    }

    [TestCase(true, CheckPointPositionAnchorKind.Above, "direct-first")]
    [TestCase(false, CheckPointPositionAnchorKind.Below, "direct-last")]
    public async Task LayerBoundary_UsesFirstAndLastRuleAcrossDirectRulesAndSections(
        bool beginning,
        CheckPointPositionAnchorKind expectedKind,
        string expectedRuleUid)
    {
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> root = beginning
            ? new BeginningOfBlock(InitialLayer())
            : new EndOfBlock(InitialLayer());

        CheckPointRulePosition position = await ExecutePosition(root, StandardContext());

        AssertPosition(position, "layer-initial", expectedKind, expectedRuleUid);
    }

    [TestCase(true, CheckPointPositionAnchorKind.Above, "app-first")]
    [TestCase(false, CheckPointPositionAnchorKind.Below, "app-last")]
    public async Task PopulatedSectionBoundary_UsesSectionRulesOnly(
        bool beginning,
        CheckPointPositionAnchorKind expectedKind,
        string expectedRuleUid)
    {
        CheckPointSectionScopeBlock section = new(
            InitialLayer(),
            CheckPointSectionSelector.ByExactName("Applications"));
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> root = beginning
            ? new BeginningOfBlock(section)
            : new EndOfBlock(section);

        CheckPointRulePosition position = await ExecutePosition(root, StandardContext());

        AssertPosition(position, "layer-initial", expectedKind, expectedRuleUid);
    }

    [TestCase(true, CheckPointPositionAnchorKind.Top)]
    [TestCase(false, CheckPointPositionAnchorKind.Bottom)]
    public async Task EmptyLayer_UsesNativeTopOrBottomAnchor(
        bool beginning,
        CheckPointPositionAnchorKind expectedKind)
    {
        Rulebase emptyLayer = StructuralRulebase(1, "empty", "Empty");
        CheckPointPositioningContext context = Context(Snapshot(
            [emptyLayer],
            [Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true)]));
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> scope = InitialLayer();
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> root = beginning
            ? new BeginningOfBlock(scope)
            : new EndOfBlock(scope);

        CheckPointRulePosition position = await ExecutePosition(root, context);

        AssertPosition(position, "empty", expectedKind, null);
    }

    [TestCase(EmptySectionLocation.First, CheckPointPositionAnchorKind.Above, "next")]
    [TestCase(EmptySectionLocation.Middle, CheckPointPositionAnchorKind.Above, "next")]
    [TestCase(EmptySectionLocation.Last, CheckPointPositionAnchorKind.Below, "previous")]
    public async Task EmptySection_UsesCanonicalBoundaryGap(
        EmptySectionLocation location,
        CheckPointPositionAnchorKind expectedKind,
        string expectedRuleUid)
    {
        CheckPointPositioningContext context = Context(EmptySectionSnapshot(location));
        CheckPointSectionScopeBlock section = new(
            InitialLayer(),
            CheckPointSectionSelector.ByUid("empty-section"));

        CheckPointRulePosition beginning = await ExecutePosition(new BeginningOfBlock(section), context);
        CheckPointRulePosition end = await ExecutePosition(new EndOfBlock(section), context);

        AssertPosition(beginning, "layer-empty-section", expectedKind, expectedRuleUid);
        AssertPosition(end, "layer-empty-section", expectedKind, expectedRuleUid);
    }

    [Test]
    public async Task OnlyEmptySection_UsesTopForItsSingleBoundaryGap()
    {
        Rulebase layer = StructuralRulebase(1, "layer-1", "Network");
        Rulebase empty = StructuralRulebase(2, "empty", "Empty");
        CheckPointPositioningContext context = Context(Snapshot(
            [layer, empty],
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                SectionLink(1, 2)
            ]));
        CheckPointSectionScopeBlock section = new(
            InitialLayer(),
            CheckPointSectionSelector.ByUid("empty"));

        CheckPointRulePosition position = await ExecutePosition(new EndOfBlock(section), context);

        AssertPosition(position, "layer-1", CheckPointPositionAnchorKind.Top, null);
    }

    [TestCase(9, kGatewayId)]
    [TestCase(kManagementId, 19)]
    public async Task ContextIdentityMismatch_ReturnsInvalidSnapshot(int managementId, int gatewayId)
    {
        CheckPointPositioningContext context = Context(StandardSnapshot(), managementId, gatewayId);

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(InitialLayer(), context);

        AssertFailure(result, PositioningBlockFailureCategory.InvalidSnapshot, CheckPointLayerScopeBlock.kKind);
    }

    [Test]
    public async Task BoundaryRejectsScopeThatDoesNotBelongToSnapshot()
    {
        Rulebase foreignLayer = StructuralRulebase(100, "foreign", "Foreign");
        RulebaseGraphSnapshot foreignSnapshot = Snapshot(
            [foreignLayer],
            [Link(null, null, 100, RulebaseLinkTypes.Ordered, isInitial: true)]);
        StaticScopeBlock malformedScope = new(new CheckPointScopeValue(foreignSnapshot.Rulebases[0]));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(
            new BeginningOfBlock(malformedScope),
            StandardContext());

        AssertFailure(result, PositioningBlockFailureCategory.NoInsertionPoint, BeginningOfBlock.kKind);
    }

    [Test]
    public void Selectors_RejectMissingSelectionValues()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => CheckPointLayerSelector.ByExactName(""));
            Assert.Throws<ArgumentException>(() => CheckPointLayerSelector.ByUid(" "));
            Assert.Throws<ArgumentException>(() => CheckPointSectionSelector.ByExactName(""));
            Assert.Throws<ArgumentException>(() => CheckPointSectionSelector.ByUid(" "));
        });
    }

    [Test]
    public async Task PolicyInitialLink_UsesItsUniqueOrderedSuccessorAsInitialLayer()
    {
        Rulebase policy = StructuralRulebase(1, "policy", "Policy");
        Rulebase accessLayer = StructuralRulebase(2, "access", "Access", StructuralRule(20, "rule", 1));
        CheckPointPositioningContext context = Context(Snapshot(
            [policy, accessLayer],
            [
                Link(null, null, 1, RulebaseLinkTypes.Policy, isInitial: true),
                Link(1, null, 2, RulebaseLinkTypes.Ordered)
            ]));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(InitialLayer(), context);

        Assert.That(SuccessScope(result).Layer.Uid, Is.EqualTo("access"));
    }

    [Test]
    public async Task DomainSuccessor_IsSelectableAsFollowingLayer()
    {
        Rulebase first = StructuralRulebase(1, "first", "First");
        Rulebase second = StructuralRulebase(2, "second", "Second");
        CheckPointPositioningContext context = Context(Snapshot(
            [first, second],
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                Link(1, null, 2, RulebaseLinkTypes.Domain)
            ]));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.ByUid("second")),
            context);

        Assert.That(SuccessScope(result).Layer.Name, Is.EqualTo("Second"));
    }

    [TestCase(InvalidGraphKind.MissingInitial)]
    [TestCase(InvalidGraphKind.MultipleInitial)]
    [TestCase(InvalidGraphKind.AmbiguousOrderedSuccessor)]
    [TestCase(InvalidGraphKind.AmbiguousSectionSuccessor)]
    [TestCase(InvalidGraphKind.MultipleInlineLinks)]
    [TestCase(InvalidGraphKind.PolicyWithoutAccessLayer)]
    public async Task InvalidCheckPointTopology_ReturnsInvalidSnapshot(InvalidGraphKind graphKind)
    {
        CheckPointPositioningContext context = Context(InvalidGraphSnapshot(graphKind));

        PositioningBlockResult<CheckPointBlockValue> result = await Execute(InitialLayer(), context);

        AssertFailure(result, PositioningBlockFailureCategory.InvalidSnapshot, CheckPointLayerScopeBlock.kKind);
    }

    [Test]
    public async Task SuppliedEntityMutation_DoesNotChangeEvaluation()
    {
        Rule anchor = StructuralRule(10, "stable-anchor", 1);
        Rulebase layer = StructuralRulebase(1, "stable-layer", "Stable", anchor);
        RulebaseLink initial = Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true);
        List<Rulebase> rulebases = [layer];
        List<RulebaseLink> links = [initial];
        RulebaseGraphSnapshot snapshot = Snapshot(rulebases, links);

        anchor.Uid = "mutated-anchor";
        layer.Uid = "mutated-layer";
        layer.Rules = [];
        initial.NextRulebaseId = 999;
        rulebases.Clear();
        links.Clear();

        CheckPointRulePosition position = await ExecutePosition(new EndOfBlock(InitialLayer()), Context(snapshot));

        AssertPosition(position, "stable-layer", CheckPointPositionAnchorKind.Below, "stable-anchor");
    }

    [Test]
    public async Task Acceptance_EndOfInitialLayer_ReturnsSemanticPosition()
    {
        EndOfBlock recipeRoot = new(InitialLayer());

        CheckPointRulePosition position = await ExecutePosition(recipeRoot, StandardContext());

        AssertPosition(position, "layer-initial", CheckPointPositionAnchorKind.Below, "direct-last");
    }

    [Test]
    public async Task Acceptance_BeginningOfNamedSection_ReturnsSemanticPosition()
    {
        BeginningOfBlock recipeRoot = new(new CheckPointSectionScopeBlock(
            InitialLayer(),
            CheckPointSectionSelector.ByExactName("Applications")));

        CheckPointRulePosition position = await ExecutePosition(recipeRoot, StandardContext());

        AssertPosition(position, "layer-initial", CheckPointPositionAnchorKind.Above, "app-first");
    }

    /// <summary>
    /// Executes a Check Point block as a complete immutable recipe.
    /// </summary>
    private static Task<PositioningBlockResult<CheckPointBlockValue>> Execute(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> root,
        CheckPointPositioningContext context)
    {
        return new PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue>()
            .ExecuteAsync(
                new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(root),
                context);
    }

    /// <summary>
    /// Executes a recipe and extracts its final semantic position.
    /// </summary>
    private static async Task<CheckPointRulePosition> ExecutePosition(
        IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> root,
        CheckPointPositioningContext context)
    {
        PositioningBlockResult<CheckPointBlockValue> result = await Execute(root, context);
        Assert.That(result, Is.TypeOf<PositioningBlockSuccess<CheckPointBlockValue>>());
        CheckPointBlockValue value = ((PositioningBlockSuccess<CheckPointBlockValue>)result).Value;
        Assert.That(value, Is.TypeOf<CheckPointPositionValue>());
        return ((CheckPointPositionValue)value).Position;
    }

    private static CheckPointScopeValue SuccessScope(PositioningBlockResult<CheckPointBlockValue> result)
    {
        return (CheckPointScopeValue)((PositioningBlockSuccess<CheckPointBlockValue>)result).Value;
    }

    private static void AssertFailure(
        PositioningBlockResult<CheckPointBlockValue> result,
        PositioningBlockFailureCategory category,
        string blockKind)
    {
        Assert.That(result, Is.TypeOf<PositioningBlockFailure<CheckPointBlockValue>>());
        PositioningBlockFailure<CheckPointBlockValue> failure =
            (PositioningBlockFailure<CheckPointBlockValue>)result;
        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(category));
            Assert.That(failure.OriginatingBlockKind, Is.EqualTo(blockKind));
            Assert.That(failure.Diagnostic, Is.Not.Empty);
        });
    }

    private static void AssertPosition(
        CheckPointRulePosition position,
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? ruleUid)
    {
        Assert.Multiple(() =>
        {
            Assert.That(position.LayerUid, Is.EqualTo(layerUid));
            Assert.That(position.AnchorKind, Is.EqualTo(anchorKind));
            Assert.That(position.AnchorRuleUid, Is.EqualTo(ruleUid));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
        });
    }

    private static CheckPointLayerScopeBlock InitialLayer() =>
        new(CheckPointLayerSelector.InitialLayer());

    private static CheckPointPositioningContext StandardContext() => Context(StandardSnapshot());

    private static CheckPointPositioningContext Context(
        RulebaseGraphSnapshot snapshot,
        int managementId = kManagementId,
        int gatewayId = kGatewayId)
    {
        RulePositioningContext calculationContext = new(
            new WfImplTask(),
            new WfReqTask(),
            new Device { Id = gatewayId },
            new Management { Id = managementId },
            new GatewayProvisioningSettings { PositioningAlgorithm = kAlgorithm });
        return new CheckPointPositioningContext(calculationContext, snapshot);
    }

    private static RulebaseGraphSnapshot StandardSnapshot()
    {
        Rulebase initial = StructuralRulebase(
            1,
            "layer-initial",
            "Network",
            StructuralRule(100, "direct-first", 1),
            StructuralRule(101, "direct-last", 4));
        Rulebase applications = StructuralRulebase(
            2,
            "section-app",
            "Applications",
            StructuralRule(200, "app-first", 2),
            StructuralRule(201, "app-last", 3));
        Rulebase inline = StructuralRulebase(3, "layer-inline", "Inline");
        Rulebase inlineSection = StructuralRulebase(4, "inline-section", "Inline section");
        return Snapshot(
            [initial, applications, inline, inlineSection],
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                SectionLink(1, 2),
                Link(1, 100, 3, RulebaseLinkTypes.Inline),
                SectionLink(3, 4)
            ]);
    }

    private static RulebaseGraphSnapshot EmptySectionSnapshot(EmptySectionLocation location)
    {
        Rulebase layer = StructuralRulebase(1, "layer-empty-section", "Network");
        Rulebase empty = StructuralRulebase(2, "empty-section", "Empty");
        Rulebase previous = StructuralRulebase(3, "previous-section", "Previous", StructuralRule(30, "previous", 1));
        Rulebase next = StructuralRulebase(4, "next-section", "Next", StructuralRule(40, "next", 1));
        List<Rulebase> rulebases = [layer, empty];
        List<RulebaseLink> links = [Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true)];
        switch (location)
        {
            case EmptySectionLocation.First:
                rulebases.Add(next);
                links.Add(SectionLink(1, 2));
                links.Add(SectionLink(2, 4));
                break;
            case EmptySectionLocation.Middle:
                rulebases.Add(previous);
                rulebases.Add(next);
                links.Add(SectionLink(1, 3));
                links.Add(SectionLink(3, 2));
                links.Add(SectionLink(2, 4));
                break;
            case EmptySectionLocation.Last:
                rulebases.Add(previous);
                links.Add(SectionLink(1, 3));
                links.Add(SectionLink(3, 2));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(location));
        }
        return Snapshot(rulebases, links);
    }

    private static RulebaseGraphSnapshot InvalidGraphSnapshot(InvalidGraphKind graphKind)
    {
        Rulebase first = StructuralRulebase(1, "first", "First", StructuralRule(10, "rule", 1));
        Rulebase second = StructuralRulebase(2, "second", "Second");
        Rulebase third = StructuralRulebase(3, "third", "Third");
        List<RulebaseLink> links = graphKind switch
        {
            InvalidGraphKind.MissingInitial => [],
            InvalidGraphKind.MultipleInitial =>
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                Link(null, null, 2, RulebaseLinkTypes.Ordered, isInitial: true)
            ],
            InvalidGraphKind.AmbiguousOrderedSuccessor =>
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                Link(1, null, 2, RulebaseLinkTypes.Ordered),
                Link(1, null, 3, RulebaseLinkTypes.Domain)
            ],
            InvalidGraphKind.AmbiguousSectionSuccessor =>
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                SectionLink(1, 2),
                SectionLink(1, 3)
            ],
            InvalidGraphKind.MultipleInlineLinks =>
            [
                Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true),
                Link(1, 10, 2, RulebaseLinkTypes.Inline),
                Link(1, 10, 3, RulebaseLinkTypes.Inline)
            ],
            InvalidGraphKind.PolicyWithoutAccessLayer =>
            [Link(null, null, 1, RulebaseLinkTypes.Policy, isInitial: true)],
            _ => throw new ArgumentOutOfRangeException(nameof(graphKind))
        };
        return Snapshot([first, second, third], links);
    }

    private static RulebaseGraphSnapshot Snapshot(
        IEnumerable<Rulebase> rulebases,
        IEnumerable<RulebaseLink> links) =>
        new(kManagementId, kGatewayId, rulebases, links);

    private static Rulebase StructuralRulebase(int id, string uid, string name, params Rule[] rules)
    {
        foreach (Rule rule in rules)
        {
            rule.MgmtId = kManagementId;
            rule.RulebaseId = id;
        }
        return new Rulebase
        {
            Id = id,
            Uid = uid,
            Name = name,
            MgmtId = kManagementId,
            Rules = rules
        };
    }

    private static Rule StructuralRule(int id, string uid, double order) =>
        new() { Id = id, Uid = uid, RuleNumNumeric = order };

    private static RulebaseLink SectionLink(int sourceRulebaseId, int targetRulebaseId) =>
        Link(sourceRulebaseId, null, targetRulebaseId, RulebaseLinkTypes.Concatenated, isSection: true);

    private static RulebaseLink Link(
        int? sourceRulebaseId,
        int? sourceRuleId,
        int targetRulebaseId,
        int linkType,
        bool isInitial = false,
        bool isSection = false) =>
        new()
        {
            GatewayId = kGatewayId,
            FromRulebaseId = sourceRulebaseId,
            FromRuleId = sourceRuleId,
            NextRulebaseId = targetRulebaseId,
            LinkType = linkType,
            IsInitial = isInitial,
            IsSection = isSection
        };

    public enum EmptySectionLocation
    {
        First,
        Middle,
        Last
    }

    public enum InvalidGraphKind
    {
        MissingInitial,
        MultipleInitial,
        AmbiguousOrderedSuccessor,
        AmbiguousSectionSuccessor,
        MultipleInlineLinks,
        PolicyWithoutAccessLayer
    }

    private sealed class StaticScopeBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
    {
        private static readonly PositioningBlockRole kScopeRole = new("check-point.scope");
        private readonly CheckPointScopeValue scope;

        public string Kind => "test.static-scope";
        public PositioningBlockRole OutputRole => kScopeRole;
        public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children =>
            Array.Empty<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>>();
        public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => Array.Empty<PositioningBlockRole>();

        public StaticScopeBlock(CheckPointScopeValue scope)
        {
            this.scope = scope;
        }

        public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
            CheckPointPositioningContext context,
            IReadOnlyList<CheckPointBlockValue> childValues,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                new PositioningBlockSuccess<CheckPointBlockValue>(scope));
        }
    }
}
