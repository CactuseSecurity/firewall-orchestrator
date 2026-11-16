using FWO.Compliance.RulePositioning;
using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using NetTools;
using NUnit.Framework;
using CheckPointLayerGuardZonePairRecipeResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.Blocks.PositioningRecipe<FWO.Data.RulePositioning.CheckPoint.CheckPointPositioningContext,
        FWO.Services.RulePositioning.CheckPoint.CheckPointBlockValue>,
    FWO.Compliance.RulePositioning.CheckPoint.CheckPointLayerGuardZonePairRecipeFailureCategory>;

namespace FWO.Test;

[TestFixture]
public class CheckPointLayerGuardZonePairRecipeTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const long kImportId = 30;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection;

    [Test]
    public async Task CompleteRecipe_SelectsGuardedLayerAndReturnsEndOfNormalizedSection()
    {
        RulebaseGraphSnapshot graph = Graph("  [source] -> [destination]  ");
        CheckPointLayerGuardZonePairPositioningInput input = Input(
            graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = Success(
            CheckPointLayerGuardZonePairRecipeFactory.Create(input, "[SOURCE] -> [DESTINATION]"));

        PositioningBlockResult<CheckPointBlockValue> result = await new PositioningRecipeExecutor<
            CheckPointPositioningContext, CheckPointBlockValue>().ExecuteAsync(
                recipe, Context(graph, input), CancellationToken.None);

        CheckPointRulePosition position = ((CheckPointPositionValue)
            ((PositioningBlockSuccess<CheckPointBlockValue>)result).Value).Position;
        Assert.Multiple(() =>
        {
            Assert.That(position.LayerUid, Is.EqualTo("inline"));
            Assert.That(position.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Bottom));
            Assert.That(position.AnchorRuleUid, Is.Null);
            Assert.That(position.SectionUid, Is.EqualTo("section"));
            Assert.That(position.SectionName, Is.EqualTo("  [source] -> [destination]  "));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
        });
    }

    [Test]
    public async Task IncorrectInteriorSectionWhitespace_IsNotNormalized()
    {
        RulebaseGraphSnapshot graph = Graph("[SOURCE]->[DESTINATION]");
        CheckPointLayerGuardZonePairPositioningInput input = Input(
            graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = Success(
            CheckPointLayerGuardZonePairRecipeFactory.Create(input, "[SOURCE] -> [DESTINATION]"));

        PositioningBlockFailure<CheckPointBlockValue> failure = (PositioningBlockFailure<CheckPointBlockValue>)
            await new PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue>()
                .ExecuteAsync(recipe, Context(graph, input));

        Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.ScopeNotFound));
        Assert.That(failure.OriginatingBlockKind, Is.EqualTo(CheckPointSectionScopeBlock.kKind));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Factory_RejectsInvalidInput(string? sectionName)
    {
        RulebaseGraphSnapshot graph = Graph("[SOURCE] -> [DESTINATION]");
        CheckPointLayerGuardZonePairPositioningInput input = Input(
            graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        Assert.Multiple(() =>
        {
            Assert.That(Failure(CheckPointLayerGuardZonePairRecipeFactory.Create(input, sectionName)),
                Is.EqualTo(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput));
            Assert.That(Failure(CheckPointLayerGuardZonePairRecipeFactory.Create(null, "SECTION")),
                Is.EqualTo(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput));
        });
    }

    [Test]
    public async Task GuardBlock_UsesAmbiguousBeforeIndeterminatePrecedence()
    {
        RulebaseGraphSnapshot graph = GraphWithTwoInlineLayers();
        CheckPointLayerGuard definiteOne = Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24");
        CheckPointLayerGuard definiteTwo = Guard(11, 1, 4, "10.0.0.0/24", "20.0.0.0/24");
        CheckPointLayerGuardZonePairPositioningInput input = Input(graph, definiteOne, definiteTwo);
        CheckPointLayerGuardScopeBlock block = new();

        PositioningBlockFailure<CheckPointBlockValue> failure = (PositioningBlockFailure<CheckPointBlockValue>)
            await block.EvaluateAsync(Context(graph, input), [], CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.ScopeAmbiguous));
    }

    [Test]
    public async Task GuardBlock_OneDefinitePlusIndeterminateFailsClosed()
    {
        RulebaseGraphSnapshot graph = GraphWithTwoInlineLayers();
        CheckPointLayerGuard definite = Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24");
        CheckPointLayerGuard indeterminate = new(
            11, 1, 4,
            new CheckPointGuardSide(false, [new CheckPointGuardAddressTerm([], [], true, false)]),
            KnownSide("20.0.0.0/24"));
        CheckPointLayerGuardZonePairPositioningInput input = Input(graph, definite, indeterminate);

        PositioningBlockFailure<CheckPointBlockValue> failure = (PositioningBlockFailure<CheckPointBlockValue>)
            await new CheckPointLayerGuardScopeBlock().EvaluateAsync(Context(graph, input), [], CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.ScopeIndeterminate));
    }

    private static PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Success(
        CheckPointLayerGuardZonePairRecipeResult result)
    {
        Assert.That(result.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe),
            Is.True);
        return recipe!;
    }

    private static CheckPointLayerGuardZonePairRecipeFailureCategory Failure(
        CheckPointLayerGuardZonePairRecipeResult result)
    {
        Assert.That(result.TryGetFailure(out CheckPointLayerGuardZonePairRecipeFailureCategory failure), Is.True);
        return failure;
    }

    private static CheckPointLayerGuardZonePairPositioningInput Input(
        RulebaseGraphSnapshot graph,
        params CheckPointLayerGuard[] guards) => new(
            new CreationRuleNetworkInput([Range("10.0.0.10")], [Range("20.0.0.10")]),
            new CheckPointLayerGuardSnapshot(graph.ManagementId, graph.GatewayId, kImportId, guards));

    private static CheckPointLayerGuard Guard(
        long sourceRuleId,
        long sourceRulebaseId,
        long targetLayerId,
        string source,
        string destination) => new(
            sourceRuleId,
            sourceRulebaseId,
            targetLayerId,
            KnownSide(source),
            KnownSide(destination));

    private static CheckPointGuardSide KnownSide(string range) => new(false,
        [new CheckPointGuardAddressTerm([Range(range)], [], false, false)]);

    private static IPAddressRange Range(string value) => IPAddressRange.Parse(value);

    private static CheckPointPositioningContext Context(
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardZonePairPositioningInput input) => new(
            new RulePositioningContext(
                new WfImplTask(),
                new WfReqTask(),
                new Device { Id = kGatewayId },
                new Management { Id = kManagementId },
                new GatewayDeviceSpecificSettings { PositioningAlgorithm = kAlgorithm }),
            graph,
            input);

    private static RulebaseGraphSnapshot Graph(string sectionName)
    {
        Rulebase main = Rulebase(1, "main", "Main", Rule(10, "guard-rule", 1));
        Rulebase inline = Rulebase(2, "inline", "Inline");
        Rulebase section = Rulebase(3, "section", sectionName, Rule(30, "section-rule", 1));
        return Snapshot([main, inline, section],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline),
            Link(2, null, 3, RulebaseLinkTypes.Concatenated, section: true)
        ]);
    }

    private static RulebaseGraphSnapshot GraphWithTwoInlineLayers()
    {
        Rulebase main = Rulebase(1, "main", "Main", Rule(10, "guard-one", 1), Rule(11, "guard-two", 2));
        return Snapshot([main, Rulebase(2, "inline-one", "One"), Rulebase(4, "inline-two", "Two")],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline),
            Link(1, 11, 4, RulebaseLinkTypes.Inline)
        ]);
    }

    private static RulebaseGraphSnapshot Snapshot(IEnumerable<Rulebase> rulebases, IEnumerable<RulebaseLink> links) =>
        new(kManagementId, kGatewayId, rulebases, links);

    private static Rulebase Rulebase(int id, string uid, string name, params Rule[] rules)
    {
        foreach (Rule rule in rules)
        {
            rule.MgmtId = kManagementId;
            rule.RulebaseId = id;
        }
        return new Rulebase { Id = id, Uid = uid, Name = name, MgmtId = kManagementId, Rules = rules };
    }

    private static Rule Rule(int id, string uid, double order) =>
        new() { Id = id, Uid = uid, RuleNumNumeric = order };

    private static RulebaseLink Link(
        int? sourceRulebaseId,
        int? sourceRuleId,
        int targetRulebaseId,
        int linkType,
        bool initial = false,
        bool section = false) => new()
        {
            GatewayId = kGatewayId,
            FromRulebaseId = sourceRulebaseId,
            FromRuleId = sourceRuleId,
            NextRulebaseId = targetRulebaseId,
            LinkType = linkType,
            IsInitial = initial,
            IsSection = section
        };
}
