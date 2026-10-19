using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using FWO.Basics.Exceptions;
using FWO.Data.Modelling;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[Parallelizable]
internal class CheckPointRulePositionRendererTest
{
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection;
    private const string kLayerUid = "layer-uid-exact";
    private const string kAnchorRuleUid = "anchor-rule-uid-exact";
    private const string kInvalidAlgorithmMessage = "The configured rule-positioning algorithm is invalid.";
    private const string kUnsupportedPositionMessage = "The rule position is not supported for Check Point rendering.";
    private const string kInvalidPositionMessage = "The Check Point rule position is invalid.";
    private const string kAlgorithmMismatchMessage = "The rule position does not match the configured positioning algorithm.";
    private const string kInvalidBodyMessage = "The Check Point access-rule request body must be a JSON object.";

    private static readonly MethodInfo RenderMethod = typeof(CheckPointTicketTask).Assembly
        .GetType("FWO.ExternalSystems.CheckPoint.CheckPointRulePositionRenderer")!
        .GetMethod("Render", BindingFlags.Static | BindingFlags.NonPublic)!;

    [TestCase(CheckPointPositionAnchorKind.Top, null, "{\"layer\":\"layer-uid-exact\",\"position\":\"top\"}")]
    [TestCase(CheckPointPositionAnchorKind.Bottom, null, "{\"layer\":\"layer-uid-exact\",\"position\":\"bottom\"}")]
    [TestCase(CheckPointPositionAnchorKind.Above, kAnchorRuleUid, "{\"layer\":\"layer-uid-exact\",\"position\":{\"above\":\"anchor-rule-uid-exact\"}}")]
    [TestCase(CheckPointPositionAnchorKind.Below, kAnchorRuleUid, "{\"layer\":\"layer-uid-exact\",\"position\":{\"below\":\"anchor-rule-uid-exact\"}}")]
    public void RenderMapsEverySemanticAnchor(
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        string expectedJson)
    {
        JsonNode rendered = Render(new JsonObject(), Position(anchorKind, anchorRuleUid), kAlgorithm);

        Assert.That(JsonNode.DeepEquals(rendered, JsonNode.Parse(expectedJson)), Is.True);
    }

    [TestCase("ordered-layer-uid")]
    [TestCase("inline-layer-uid")]
    public void RenderUsesLayerForOrderedAndInlineLayers(string layerUid)
    {
        JsonObject rendered = (JsonObject)Render(
            new JsonObject(),
            Position(CheckPointPositionAnchorKind.Top, layerUid: layerUid),
            kAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(rendered["layer"]!.GetValue<string>(), Is.EqualTo(layerUid));
            Assert.That(rendered.ContainsKey("inline-layer"), Is.False);
        });
    }

    [Test]
    public void RenderPreservesAnchorUidExactlyAndUsesScalarBoundaryAnchors()
    {
        const string exactUid = "  Rule-UID/With Case  ";

        JsonNode above = Render(new JsonObject(), Position(CheckPointPositionAnchorKind.Above, exactUid), kAlgorithm);
        JsonNode below = Render(new JsonObject(), Position(CheckPointPositionAnchorKind.Below, exactUid), kAlgorithm);
        JsonNode top = Render(new JsonObject(), Position(CheckPointPositionAnchorKind.Top), kAlgorithm);
        JsonNode bottom = Render(new JsonObject(), Position(CheckPointPositionAnchorKind.Bottom), kAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(above["position"]!["above"]!.GetValue<string>(), Is.EqualTo(exactUid));
            Assert.That(above["position"]!.AsObject().Count, Is.EqualTo(1));
            Assert.That(below["position"]!["below"]!.GetValue<string>(), Is.EqualTo(exactUid));
            Assert.That(below["position"]!.AsObject().Count, Is.EqualTo(1));
            Assert.That(top["position"]!.GetValue<string>(), Is.EqualTo("top"));
            Assert.That(bottom["position"]!.GetValue<string>(), Is.EqualTo("bottom"));
        });
    }

    [Test]
    public void RenderReplacesPlacementFieldsInTemplateCreatedTaskBodyOnly()
    {
        CheckPointTicketTask ticketTask = new(
            new WfReqTask { TaskType = WfTaskType.access.ToString(), TaskNumber = 17 },
            [],
            new ModellingNamingConvention());
        ticketTask.FillTaskText(new()
        {
            TicketTemplate = "{\"name\":\"rule-name\",\"enabled\":true,\"source\":[\"a\",\"b\"]," +
                "\"details\":{\"comment\":\"keep\",\"nested\":[1,{\"value\":2}]}," +
                "\"layer\":\"template-layer\",\"position\":{\"above\":\"template-rule\"}}"
        });
        JsonNode original = ticketTask.TaskBody.DeepClone();

        JsonNode rendered = Render(
            ticketTask.TaskBody,
            Position(CheckPointPositionAnchorKind.Below, kAnchorRuleUid),
            kAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(rendered["name"]!.GetValue<string>(), Is.EqualTo("rule-name"));
            Assert.That(rendered["enabled"]!.GetValue<bool>(), Is.True);
            Assert.That(JsonNode.DeepEquals(rendered["source"], JsonNode.Parse("[\"a\",\"b\"]")), Is.True);
            Assert.That(JsonNode.DeepEquals(rendered["details"], original["details"]), Is.True);
            Assert.That(rendered["layer"]!.GetValue<string>(), Is.EqualTo(kLayerUid));
            Assert.That(rendered["position"]!["below"]!.GetValue<string>(), Is.EqualTo(kAnchorRuleUid));
            Assert.That(JsonNode.DeepEquals(ticketTask.TaskBody, original), Is.True);
        });
    }

    [Test]
    public void RenderWithNullPositionClonesObjectWithoutValidatingAlgorithm()
    {
        JsonNode original = JsonNode.Parse("{\"layer\":\"legacy\",\"position\":\"template\",\"nested\":{\"x\":1}}")!;

        JsonNode rendered = Render(original, null, ProvisioningPositioningAlgorithm.Undefined);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Is.Not.SameAs(original));
            Assert.That(JsonNode.DeepEquals(rendered, original), Is.True);
        });
    }

    [Test]
    public void RenderWithNullPositionClonesNonObjectWithoutValidatingAlgorithm()
    {
        JsonNode original = JsonNode.Parse("[1,{\"legacy\":true}]")!;

        JsonNode rendered = Render(original, null, (ProvisioningPositioningAlgorithm)999);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Is.Not.SameAs(original));
            Assert.That(JsonNode.DeepEquals(rendered, original), Is.True);
        });
    }

    [Test]
    public void RenderRejectsUnknownAndNonCheckPointPositions()
    {
        UnknownRulePosition unknown = new(
            "future.vendor-position",
            1,
            kAlgorithm,
            new JsonObject { ["credential"] = "unknown-secret" });
        TestRulePosition wrongVendor = new(kAlgorithm);

        AssertConfigException(() => Render(new JsonObject(), unknown, kAlgorithm), kUnsupportedPositionMessage);
        AssertConfigException(() => Render(new JsonObject(), wrongVendor, kAlgorithm), kUnsupportedPositionMessage);
    }

    [TestCase(ProvisioningPositioningAlgorithm.Undefined)]
    [TestCase((ProvisioningPositioningAlgorithm)999)]
    public void RenderRejectsInvalidExpectedAlgorithm(ProvisioningPositioningAlgorithm expectedAlgorithm)
    {
        AssertConfigException(
            () => Render(new JsonObject(), Position(CheckPointPositionAnchorKind.Top), expectedAlgorithm),
            kInvalidAlgorithmMessage);
    }

    [Test]
    public void RenderRejectsAlgorithmMismatch()
    {
        AssertConfigException(
            () => Render(
                new JsonObject(),
                Position(CheckPointPositionAnchorKind.Top),
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase),
            kAlgorithmMismatchMessage);
    }

    [Test]
    public void RenderRejectsEveryMalformedCheckPointPosition()
    {
        CheckPointRulePosition wrongSchema = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(wrongSchema, nameof(RulePosition.SchemaVersion), 2);

        CheckPointRulePosition blankLayer = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(blankLayer, nameof(CheckPointRulePosition.LayerUid), "   ");

        CheckPointRulePosition unknownAnchor = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(unknownAnchor, nameof(CheckPointRulePosition.AnchorKind), (CheckPointPositionAnchorKind)999);

        CheckPointRulePosition missingRuleUid = Position(CheckPointPositionAnchorKind.Above, kAnchorRuleUid);
        SetProperty(missingRuleUid, nameof(CheckPointRulePosition.AnchorRuleUid), " ");

        CheckPointRulePosition prohibitedRuleUid = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(prohibitedRuleUid, nameof(CheckPointRulePosition.AnchorRuleUid), kAnchorRuleUid);

        CheckPointRulePosition undefinedAlgorithm = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(undefinedAlgorithm, nameof(RulePosition.PositioningAlgorithm), ProvisioningPositioningAlgorithm.Undefined);

        CheckPointRulePosition unknownAlgorithm = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(unknownAlgorithm, nameof(RulePosition.PositioningAlgorithm), (ProvisioningPositioningAlgorithm)999);

        CheckPointRulePosition wrongDiscriminator = Position(CheckPointPositionAnchorKind.Top);
        SetProperty(wrongDiscriminator, nameof(RulePosition.PositionType), "wrong-discriminator");

        foreach (CheckPointRulePosition malformed in new[]
        {
            wrongSchema,
            blankLayer,
            unknownAnchor,
            missingRuleUid,
            prohibitedRuleUid,
            undefinedAlgorithm,
            unknownAlgorithm,
            wrongDiscriminator
        })
        {
            AssertConfigException(() => Render(new JsonObject(), malformed, kAlgorithm), kInvalidPositionMessage);
        }
    }

    [Test]
    public void RenderRejectsNonObjectBodyForNonNullPosition()
    {
        AssertConfigException(
            () => Render(JsonValue.Create("raw-body-secret"), Position(CheckPointPositionAnchorKind.Top), kAlgorithm),
            kInvalidBodyMessage);
    }

    [Test]
    public void RenderIsIdempotentAndKeepsSinglePlacementProperties()
    {
        CheckPointRulePosition position = Position(CheckPointPositionAnchorKind.Above, kAnchorRuleUid);
        JsonNode first = Render(
            JsonNode.Parse("{\"layer\":\"old\",\"position\":\"old\",\"name\":\"rule\"}")!,
            position,
            kAlgorithm);

        JsonNode second = Render(first, position, kAlgorithm);
        JsonObject secondObject = second.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.DeepEquals(second, first), Is.True);
            Assert.That(secondObject.Count(property => property.Key == "layer"), Is.EqualTo(1));
            Assert.That(secondObject.Count(property => property.Key == "position"), Is.EqualTo(1));
        });
    }

    [Test]
    public void RenderDoesNotMutateInputBodyOrPosition()
    {
        JsonNode original = JsonNode.Parse("{\"name\":\"rule\",\"nested\":{\"items\":[1,2]}}")!;
        JsonNode originalSnapshot = original.DeepClone();
        CheckPointRulePosition position = Position(CheckPointPositionAnchorKind.Below, kAnchorRuleUid);

        JsonNode rendered = Render(original, position, kAlgorithm);
        rendered["nested"]!["items"]!.AsArray().Add(3);

        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.DeepEquals(original, originalSnapshot), Is.True);
            Assert.That(position.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
            Assert.That(position.SchemaVersion, Is.EqualTo(1));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
            Assert.That(position.LayerUid, Is.EqualTo(kLayerUid));
            Assert.That(position.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Below));
            Assert.That(position.AnchorRuleUid, Is.EqualTo(kAnchorRuleUid));
        });
    }

    [Test]
    public void RenderDiagnosticsDoNotExposeSensitiveValues()
    {
        const string sentinelLayer = "sentinel-layer-uid";
        const string sentinelRule = "sentinel-rule-uid";
        const string sentinelCredential = "credential-secret";
        CheckPointRulePosition position = Position(
            CheckPointPositionAnchorKind.Above,
            sentinelRule,
            sentinelLayer);
        UnknownRulePosition unknown = new(
            "future-position",
            1,
            kAlgorithm,
            new JsonObject { ["credential"] = sentinelCredential });

        List<string> messages =
        [
            CaptureConfigMessage(() => Render(new JsonObject(), position, ProvisioningPositioningAlgorithm.Undefined)),
            CaptureConfigMessage(() => Render(new JsonObject(), unknown, kAlgorithm)),
            CaptureConfigMessage(() => Render(new JsonObject(), position, ProvisioningPositioningAlgorithm.DefaultEndOfRulebase)),
            CaptureConfigMessage(() => Render(JsonValue.Create("raw-body-secret"), position, kAlgorithm))
        ];

        foreach (string message in messages)
        {
            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Not.Contain(sentinelLayer));
                Assert.That(message, Does.Not.Contain(sentinelRule));
                Assert.That(message, Does.Not.Contain(sentinelCredential));
                Assert.That(message, Does.Not.Contain("raw-body-secret"));
                Assert.That(message, Does.Not.Contain(kAlgorithm.ToString()));
                Assert.That(message, Does.Not.Contain(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase.ToString()));
            });
        }
    }

    [Test]
    public void RenderRejectsNullRequestBodyBeforeNullPositionHandling()
    {
        Assert.Throws<ArgumentNullException>(() => Render(null!, null, ProvisioningPositioningAlgorithm.Undefined));
    }

    private static CheckPointRulePosition Position(
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid = null,
        string layerUid = kLayerUid)
    {
        return new CheckPointRulePosition(layerUid, anchorKind, anchorRuleUid, kAlgorithm);
    }

    private static JsonNode Render(
        JsonNode requestBody,
        RulePosition? rulePosition,
        ProvisioningPositioningAlgorithm expectedAlgorithm)
    {
        try
        {
            return (JsonNode)RenderMethod.Invoke(null, [requestBody, rulePosition, expectedAlgorithm])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? typeof(RulePosition).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property {propertyName} was not found.");
        property.SetValue(target, value);
    }

    private static void AssertConfigException(TestDelegate action, string expectedMessage)
    {
        ConfigException exception = Assert.Throws<ConfigException>(action)!;
        Assert.That(exception.Message, Is.EqualTo(expectedMessage));
    }

    private static string CaptureConfigMessage(TestDelegate action)
    {
        return Assert.Throws<ConfigException>(action)!.Message;
    }

    [RulePositionType("test.rule-position.v1")]
    private sealed class TestRulePosition : RulePosition
    {
        public TestRulePosition(ProvisioningPositioningAlgorithm algorithm)
        {
            PositioningAlgorithm = algorithm;
        }
    }
}
