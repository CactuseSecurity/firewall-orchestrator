using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointPositioningContractsTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection;
    private static readonly string[] kExpectedRulebaseUids = ["layer-1", "section-1", "inline-1"];
    private static readonly string[] kExpectedRuleUids = ["earlier", "later"];
    private static readonly int[] kExpectedNativeOrder = [1, 2];
    private static readonly long[] kExpectedTargetRulebaseIds = [1, 2, 3];

    [Test]
    public void Snapshot_DefensivelyCopiesEntitiesCollectionsAndNativeOrder()
    {
        Rule laterRule = StructuralRule(101, "later", 2);
        Rule earlierRule = StructuralRule(100, "earlier", 1);
        Rulebase layer = StructuralRulebase(1, "layer-1", "Network", laterRule, earlierRule);
        Rulebase section = StructuralRulebase(2, "section-1", "Applications", StructuralRule(200, "section-rule", 1));
        Rulebase inline = StructuralRulebase(3, "inline-1", "Inline", StructuralRule(300, "inline-rule", 1));
        RulebaseLink initialLink = Link(null, null, 1, RulebaseLinkTypes.Ordered, isInitial: true);
        initialLink.IsGlobal = true;
        RulebaseLink sectionLink = Link(1, null, 2, RulebaseLinkTypes.Concatenated, isSection: true);
        RulebaseLink inlineLink = Link(1, 100, 3, RulebaseLinkTypes.Inline);
        List<Rulebase> rulebases = [layer, section, inline];
        List<RulebaseLink> links = [initialLink, sectionLink, inlineLink];

        RulebaseGraphSnapshot snapshot = new(kManagementId, kGatewayId, rulebases, links);

        laterRule.Uid = "changed-rule";
        earlierRule.RuleNumNumeric = 99;
        layer.Uid = "changed-layer";
        layer.Name = "Changed";
        layer.Rules = [];
        initialLink.NextRulebaseId = 999;
        initialLink.IsInitial = false;
        initialLink.IsGlobal = false;
        sectionLink.IsSection = false;
        rulebases.Clear();
        links.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Rulebases.Select(rulebase => rulebase.Uid),
                Is.EqualTo(kExpectedRulebaseUids));
            Assert.That(snapshot.Rulebases[0].Name, Is.EqualTo("Network"));
            Assert.That(snapshot.Rulebases[0].Rules.Select(rule => rule.Uid),
                Is.EqualTo(kExpectedRuleUids));
            Assert.That(snapshot.Rulebases[0].Rules.Select(rule => rule.NativeOrder),
                Is.EqualTo(kExpectedNativeOrder));
            Assert.That(snapshot.Links.Select(link => link.TargetRulebaseId),
                Is.EqualTo(kExpectedTargetRulebaseIds));
            Assert.That(snapshot.Links[0].LinkType, Is.EqualTo(RulebaseLinkTypes.Ordered));
            Assert.That(snapshot.Links[0].IsInitial, Is.True);
            Assert.That(snapshot.Links[0].IsGlobal, Is.True);
            Assert.That(snapshot.Links[1].IsSection, Is.True);
            Assert.That(snapshot.Links[2].SourceRuleId, Is.EqualTo(100));
        });
    }

    [TestCase(0, kGatewayId)]
    [TestCase(kManagementId, 0)]
    [TestCase(-1, kGatewayId)]
    [TestCase(kManagementId, -1)]
    public void Snapshot_RejectsInvalidManagementOrGatewayIdentity(int managementId, int gatewayId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RulebaseGraphSnapshot(managementId, gatewayId, [], []));
    }

    [Test]
    public void Snapshot_RejectsMismatchedEntityOwnership()
    {
        Rulebase wrongManagement = StructuralRulebase(1, "layer", "Layer");
        wrongManagement.MgmtId = 11;
        Rulebase wrongRuleManagement = StructuralRulebase(2, "layer-2", "Layer 2", StructuralRule(20, "rule-20", 1));
        wrongRuleManagement.Rules[0].MgmtId = 11;
        Rulebase wrongRulebase = StructuralRulebase(3, "layer-3", "Layer 3", StructuralRule(30, "rule-30", 1));
        wrongRulebase.Rules[0].RulebaseId = 4;
        RulebaseLink wrongGateway = Link(null, null, 5, RulebaseLinkTypes.Ordered, isInitial: true);
        wrongGateway.GatewayId = 21;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Snapshot([wrongManagement], []));
            Assert.Throws<ArgumentException>(() => Snapshot([wrongRuleManagement], []));
            Assert.Throws<ArgumentException>(() => Snapshot([wrongRulebase], []));
            Assert.Throws<ArgumentException>(() => Snapshot(
                [StructuralRulebase(5, "layer-5", "Layer 5")],
                [wrongGateway]));
        });
    }

    [Test]
    public void Snapshot_RejectsDuplicateRulebaseAndRuleIdentities()
    {
        Rulebase duplicateId = StructuralRulebase(1, "other", "Other");
        Rulebase duplicateUid = StructuralRulebase(2, "layer", "Other");
        Rulebase duplicateRuleId = StructuralRulebase(3, "third", "Third", StructuralRule(10, "rule-2", 1));
        Rulebase duplicateRuleUid = StructuralRulebase(4, "fourth", "Fourth", StructuralRule(11, "rule-1", 1));
        Rulebase first = StructuralRulebase(1, "layer", "Layer", StructuralRule(10, "rule-1", 1));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Snapshot([first, duplicateId], []));
            Assert.Throws<ArgumentException>(() => Snapshot([first, duplicateUid], []));
            Assert.Throws<ArgumentException>(() => Snapshot([first, duplicateRuleId], []));
            Assert.Throws<ArgumentException>(() => Snapshot([first, duplicateRuleUid], []));
        });
    }

    [Test]
    public void Snapshot_RejectsMalformedIdentityAndNativeOrder()
    {
        Rulebase blankRulebaseUid = StructuralRulebase(1, " ", "Layer");
        Rulebase blankRuleUid = StructuralRulebase(2, "layer-2", "Layer 2", StructuralRule(20, "", 1));
        Rulebase duplicateOrder = StructuralRulebase(
            3,
            "layer-3",
            "Layer 3",
            StructuralRule(30, "rule-30", 1),
            StructuralRule(31, "rule-31", 1));
        Rulebase invalidOrder = StructuralRulebase(4, "layer-4", "Layer 4", StructuralRule(40, "rule-40", double.NaN));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Snapshot([blankRulebaseUid], []));
            Assert.Throws<ArgumentException>(() => Snapshot([blankRuleUid], []));
            Assert.Throws<ArgumentException>(() => Snapshot([duplicateOrder], []));
            Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot([invalidOrder], []));
        });
    }

    [Test]
    public void Snapshot_RejectsMissingLinkEndpoints()
    {
        Rulebase layer = StructuralRulebase(1, "layer", "Layer", StructuralRule(10, "rule", 1));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Snapshot(
                [layer],
                [Link(null, null, 999, RulebaseLinkTypes.Ordered, isInitial: true)]));
            Assert.Throws<ArgumentException>(() => Snapshot(
                [layer],
                [Link(999, null, 1, RulebaseLinkTypes.Ordered)]));
            Assert.Throws<ArgumentException>(() => Snapshot(
                [layer],
                [Link(1, 999, 1, RulebaseLinkTypes.Inline)]));
            Assert.Throws<ArgumentException>(() => Snapshot(
                [layer],
                [Link(null, null, 1, RulebaseLinkTypes.Ordered)]));
        });
    }

    [Test]
    public void Snapshot_RejectsRuleSourceOwnedByAnotherRulebase()
    {
        Rulebase first = StructuralRulebase(1, "first", "First", StructuralRule(10, "rule", 1));
        Rulebase second = StructuralRulebase(2, "second", "Second");

        Assert.Throws<ArgumentException>(() => Snapshot(
            [first, second],
            [Link(2, 10, 2, RulebaseLinkTypes.Inline)]));
    }

    [TestCase(CheckPointPositionAnchorKind.Top, null)]
    [TestCase(CheckPointPositionAnchorKind.Bottom, null)]
    [TestCase(CheckPointPositionAnchorKind.Above, "rule-1")]
    [TestCase(CheckPointPositionAnchorKind.Below, "rule-1")]
    public void Position_AcceptsValidAnchorCombinations(
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid)
    {
        CheckPointRulePosition position = new("layer-1", anchorKind, anchorRuleUid, kAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(position.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
            Assert.That(position.LayerUid, Is.EqualTo("layer-1"));
            Assert.That(position.AnchorKind, Is.EqualTo(anchorKind));
            Assert.That(position.AnchorRuleUid, Is.EqualTo(anchorRuleUid));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
        });
    }

    [TestCase(CheckPointPositionAnchorKind.Top, "rule-1")]
    [TestCase(CheckPointPositionAnchorKind.Bottom, "rule-1")]
    [TestCase(CheckPointPositionAnchorKind.Above, null)]
    [TestCase(CheckPointPositionAnchorKind.Below, null)]
    [TestCase(CheckPointPositionAnchorKind.Above, " ")]
    public void Position_RejectsInvalidAnchorCombinations(
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid)
    {
        Assert.Throws<ArgumentException>(() =>
            new CheckPointRulePosition("layer-1", anchorKind, anchorRuleUid, kAlgorithm));
    }

    [Test]
    public void Position_RejectsInvalidLayerAndAlgorithm()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new CheckPointRulePosition(
                "",
                CheckPointPositionAnchorKind.Top,
                null,
                kAlgorithm));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Top,
                null,
                ProvisioningPositioningAlgorithm.Undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Top,
                null,
                (ProvisioningPositioningAlgorithm)999));
        });
    }

    [Test]
    public void Position_SystemTextJsonRoundTripsThroughBaseType()
    {
        AssertRoundTrip(
            position => System.Text.Json.JsonSerializer.Serialize(position),
            json => System.Text.Json.JsonSerializer.Deserialize<RulePosition>(json));
    }

    [Test]
    public void Position_NewtonsoftJsonRoundTripsThroughBaseType()
    {
        AssertRoundTrip(
            JsonConvert.SerializeObject,
            JsonConvert.DeserializeObject<RulePosition>);
    }

    [Test]
    public void Position_RoundTripsInsidePersistedImplementationTaskWithBothSerializers()
    {
        WfImplTask task = new()
        {
            Id = 42,
            RulePosition = new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Below,
                "rule-1",
                kAlgorithm)
        };

        WfImplTask systemTextCopy = System.Text.Json.JsonSerializer.Deserialize<WfImplTask>(
            System.Text.Json.JsonSerializer.Serialize(task))!;
        WfImplTask newtonsoftCopy = JsonConvert.DeserializeObject<WfImplTask>(
            JsonConvert.SerializeObject(task))!;

        Assert.Multiple(() =>
        {
            Assert.That(systemTextCopy.RulePosition, Is.TypeOf<CheckPointRulePosition>());
            Assert.That(newtonsoftCopy.RulePosition, Is.TypeOf<CheckPointRulePosition>());
            Assert.That(systemTextCopy.Id, Is.EqualTo(42));
            Assert.That(newtonsoftCopy.Id, Is.EqualTo(42));
        });
    }

    [Test]
    public void Position_DeepCopyProducesIndependentKnownSubtype()
    {
        CheckPointRulePosition original = new(
            "layer-1",
            CheckPointPositionAnchorKind.Below,
            "rule-1",
            kAlgorithm);

        CheckPointRulePosition copy = (CheckPointRulePosition)original.DeepCopy();

        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.LayerUid, Is.EqualTo(original.LayerUid));
            Assert.That(copy.AnchorKind, Is.EqualTo(original.AnchorKind));
            Assert.That(copy.AnchorRuleUid, Is.EqualTo(original.AnchorRuleUid));
            Assert.That(copy.PositioningAlgorithm, Is.EqualTo(original.PositioningAlgorithm));
        });
    }

    /// <summary>
    /// Verifies polymorphic persistence fields through one serializer pair.
    /// </summary>
    private static void AssertRoundTrip(
        Func<RulePosition, string> serialize,
        Func<string, RulePosition?> deserialize)
    {
        RulePosition original = new CheckPointRulePosition(
            "layer-1",
            CheckPointPositionAnchorKind.Above,
            "rule-1",
            kAlgorithm);

        string json = serialize(original);
        RulePosition? result = deserialize(json);

        Assert.That(result, Is.TypeOf<CheckPointRulePosition>());
        CheckPointRulePosition position = (CheckPointRulePosition)result!;
        Assert.Multiple(() =>
        {
            Assert.That(position.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
            Assert.That(position.SchemaVersion, Is.EqualTo(1));
            Assert.That(position.LayerUid, Is.EqualTo("layer-1"));
            Assert.That(position.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Above));
            Assert.That(position.AnchorRuleUid, Is.EqualTo("rule-1"));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
            Assert.That(json, Does.Not.Contain("$type"));
        });
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
}
