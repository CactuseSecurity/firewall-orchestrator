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

namespace FWO.Test;

[TestFixture]
public class KfwCheckPointPositioningRecipeTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const long kImportId = 30;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection;

    [TestCase(" destination ", " source ", "[DESTINATION] < [SOURCE]")]
    [TestCase("same", "same", "[SAME] < [SAME]")]
    public void SectionName_BuildsCanonicalDestinationFirstValue(string destination, string source, string expected)
    {
        Assert.That(CheckPointSectionName.TryBuild(destination, source, out string value), Is.True);
        Assert.That(value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("bad[name")]
    [TestCase("bad]name")]
    [TestCase("bad<name")]
    public void SectionName_RejectsInvalidZoneName(string invalid)
    {
        Assert.That(CheckPointSectionName.TryBuild(invalid, "source", out string value), Is.False);
        Assert.That(value, Is.Empty);
    }

    [Test]
    public async Task CompleteRecipe_SelectsGuardedLayerAndReturnsEndOfNormalizedSection()
    {
        RulebaseGraphSnapshot graph = Graph("  [destination] < [source]  ");
        KfwCheckPointPositioningInput input = Input(graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        CreationRuleZoneInput zones = Zones(
            [Zone(1, "source")],
            [Zone(2, "destination")]);
        KfwCheckPointPositioningRecipeSuccess created = (KfwCheckPointPositioningRecipeSuccess)
            KfwCheckPointPositioningRecipeFactory.Create(input, zones);

        PositioningBlockResult<CheckPointBlockValue> result = await new PositioningRecipeExecutor<
            CheckPointPositioningContext, CheckPointBlockValue>().ExecuteAsync(
                created.Recipe, Context(graph, input), CancellationToken.None);

        CheckPointRulePosition position = ((CheckPointPositionValue)
            ((PositioningBlockSuccess<CheckPointBlockValue>)result).Value).Position;
        Assert.Multiple(() =>
        {
            Assert.That(position.LayerUid, Is.EqualTo("inline"));
            Assert.That(position.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Below));
            Assert.That(position.AnchorRuleUid, Is.EqualTo("section-rule"));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
        });
    }

    [Test]
    public async Task IncorrectInteriorSectionWhitespace_IsNotNormalized()
    {
        RulebaseGraphSnapshot graph = Graph("[DESTINATION]<[SOURCE]");
        KfwCheckPointPositioningInput input = Input(graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        KfwCheckPointPositioningRecipeSuccess created = (KfwCheckPointPositioningRecipeSuccess)
            KfwCheckPointPositioningRecipeFactory.Create(input,
                Zones([Zone(1, "source")], [Zone(2, "destination")]));

        PositioningBlockFailure<CheckPointBlockValue> failure = (PositioningBlockFailure<CheckPointBlockValue>)
            await new PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue>()
                .ExecuteAsync(created.Recipe, Context(graph, input));

        Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.ScopeNotFound));
        Assert.That(failure.OriginatingBlockKind, Is.EqualTo(CheckPointSectionScopeBlock.kKind));
    }

    [TestCase(ZoneCoverage.Partial, ZoneCoverage.Complete, KfwCheckPointPositioningRecipeFailureCategory.ZoneCoverageInvalid)]
    [TestCase(ZoneCoverage.Complete, ZoneCoverage.None, KfwCheckPointPositioningRecipeFailureCategory.ZoneCoverageInvalid)]
    public void Factory_RejectsIncompleteCoverage(
        ZoneCoverage sourceCoverage,
        ZoneCoverage destinationCoverage,
        KfwCheckPointPositioningRecipeFailureCategory expected)
    {
        RulebaseGraphSnapshot graph = Graph("[DESTINATION] < [SOURCE]");
        KfwCheckPointPositioningInput input = Input(graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        CreationRuleZoneInput zones = new(1, [Zone(1, "source")], [Zone(2, "destination")],
            sourceCoverage, destinationCoverage);

        KfwCheckPointPositioningRecipeFailure failure = (KfwCheckPointPositioningRecipeFailure)
            KfwCheckPointPositioningRecipeFactory.Create(input, zones);

        Assert.That(failure.Category, Is.EqualTo(expected));
    }

    [Test]
    public void Factory_EnforcesCardinalityPersistenceAndZoneData()
    {
        RulebaseGraphSnapshot graph = Graph("[DESTINATION] < [SOURCE]");
        KfwCheckPointPositioningInput input = Input(graph, Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"));
        Assert.Multiple(() =>
        {
            Assert.That(Failure(KfwCheckPointPositioningRecipeFactory.Create(input, Zones([], [Zone(2, "destination")]))).Category,
                Is.EqualTo(KfwCheckPointPositioningRecipeFailureCategory.ZoneCardinalityInvalid));
            Assert.That(Failure(KfwCheckPointPositioningRecipeFactory.Create(input, Zones(
                [new PositioningZoneReference(null, "x", "source", false, false, PositioningZoneKind.SyntheticInternetFallback)],
                [Zone(2, "destination")]))).Category,
                Is.EqualTo(KfwCheckPointPositioningRecipeFailureCategory.ZoneNotPersisted));
            Assert.That(Failure(KfwCheckPointPositioningRecipeFactory.Create(input, Zones([Zone(1, "bad[name")], [Zone(2, "destination")]))).Category,
                Is.EqualTo(KfwCheckPointPositioningRecipeFailureCategory.ZoneDataInvalid));
            Assert.That(Failure(KfwCheckPointPositioningRecipeFactory.Create(null, Zones([Zone(1, "source")], [Zone(2, "destination")]))).Category,
                Is.EqualTo(KfwCheckPointPositioningRecipeFailureCategory.InvalidInput));
        });
    }

    [Test]
    public async Task GuardBlock_UsesAmbiguousBeforeIndeterminatePrecedence()
    {
        RulebaseGraphSnapshot graph = GraphWithTwoInlineLayers();
        CheckPointLayerGuard definiteOne = Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24");
        CheckPointLayerGuard definiteTwo = Guard(11, 1, 4, "10.0.0.0/24", "20.0.0.0/24");
        KfwCheckPointPositioningInput input = Input(graph, definiteOne, definiteTwo);
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
        KfwCheckPointPositioningInput input = Input(graph, definite, indeterminate);

        PositioningBlockFailure<CheckPointBlockValue> failure = (PositioningBlockFailure<CheckPointBlockValue>)
            await new CheckPointLayerGuardScopeBlock().EvaluateAsync(Context(graph, input), [], CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.ScopeIndeterminate));
    }

    private static KfwCheckPointPositioningRecipeFailure Failure(KfwCheckPointPositioningRecipeResult result) =>
        (KfwCheckPointPositioningRecipeFailure)result;

    private static PositioningZoneReference Zone(int id, string name) =>
        new(id, id.ToString(), name, false, false, PositioningZoneKind.Persisted);

    private static CreationRuleZoneInput Zones(
        IReadOnlyCollection<PositioningZoneReference> source,
        IReadOnlyCollection<PositioningZoneReference> destination) =>
        new(1, source, destination, ZoneCoverage.Complete, ZoneCoverage.Complete);

    private static KfwCheckPointPositioningInput Input(
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
        KfwCheckPointPositioningInput input) => new(
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
