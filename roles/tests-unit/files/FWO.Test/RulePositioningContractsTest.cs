using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class RulePositioningContractsTest
{
    private const string kKnownDiscriminator = "test.mutable-position.v1";
    private const string kUnknownDiscriminator = "future.vendor-position.v3";
    private const string kSensitiveCredential = "credential-secret-4711";
    private const string kCompleteRuleContent = "complete-rule-content-0815";
    private const string kUnknownJson = """
        {
          "position_type": "future.vendor-position.v3",
          "schema_version": 7,
          "positioning_algorithm": 2,
          "vendor_data": {
            "section": "Future section",
            "markers": ["first", "second"]
          },
          "future_flag": true
        }
        """;

    [Test]
    public void SystemTextJson_RoundTripsKnownDerivedPosition()
    {
        RulePosition original = KnownPosition();

        string json = System.Text.Json.JsonSerializer.Serialize(original);
        RulePosition? deserialized = System.Text.Json.JsonSerializer.Deserialize<RulePosition>(json);

        AssertStableDiscriminator(json);
        AssertKnownPosition(deserialized);
    }

    [Test]
    public void NewtonsoftJson_RoundTripsKnownDerivedPosition()
    {
        RulePosition original = KnownPosition();

        string json = JsonConvert.SerializeObject(original);
        RulePosition? deserialized = JsonConvert.DeserializeObject<RulePosition>(json);

        AssertStableDiscriminator(json);
        AssertKnownPosition(deserialized);
    }

    [Test]
    public void SystemTextJson_LoadsUnknownPositionInsideParentObject()
    {
        string json = $$"""{"name":"parent","position":{{kUnknownJson}}}""";

        PositionEnvelope? envelope = System.Text.Json.JsonSerializer.Deserialize<PositionEnvelope>(json);

        AssertUnknownPosition(envelope?.Position);
    }

    [Test]
    public void NewtonsoftJson_LoadsUnknownPositionInsideParentObject()
    {
        string json = $$"""{"name":"parent","position":{{kUnknownJson}}}""";

        PositionEnvelope? envelope = JsonConvert.DeserializeObject<PositionEnvelope>(json);

        AssertUnknownPosition(envelope?.Position);
    }

    [Test]
    public void SystemTextJson_PreservesUnknownContentAcrossRoundTrip()
    {
        RulePosition position = System.Text.Json.JsonSerializer.Deserialize<RulePosition>(kUnknownJson)!;

        string serialized = System.Text.Json.JsonSerializer.Serialize(position);
        JsonObject payload = JsonNode.Parse(serialized)!.AsObject();

        AssertUnknownContent(payload);
    }

    [Test]
    public void NewtonsoftJson_PreservesUnknownContentAcrossRoundTrip()
    {
        RulePosition position = JsonConvert.DeserializeObject<RulePosition>(kUnknownJson)!;

        string serialized = JsonConvert.SerializeObject(position);
        JsonObject payload = JsonNode.Parse(serialized)!.AsObject();

        AssertUnknownContent(payload);
    }

    [Test]
    public void DeepCopy_KnownPositionDoesNotShareMutablePayload()
    {
        MutableTestRulePosition original = (MutableTestRulePosition)KnownPosition();

        MutableTestRulePosition copy = (MutableTestRulePosition)original.DeepCopy();
        original.Markers.Add("changed-after-copy");

        Assert.That(copy, Is.Not.SameAs(original));
        Assert.That(copy.Markers, Is.Not.SameAs(original.Markers));
        Assert.That(copy.Markers, Is.EqualTo(new List<string> { "alpha", "beta" }));
    }

    [Test]
    public void DeepCopy_UnknownPositionDoesNotShareRawPayload()
    {
        UnknownRulePosition original = (UnknownRulePosition)
            System.Text.Json.JsonSerializer.Deserialize<RulePosition>(kUnknownJson)!;

        UnknownRulePosition copy = (UnknownRulePosition)original.DeepCopy();
        original.RawPayload["vendor_data"]!["markers"]!.AsArray().Add("changed-after-copy");

        Assert.That(copy, Is.Not.SameAs(original));
        Assert.That(copy.RawPayload, Is.Not.SameAs(original.RawPayload));
        Assert.That(copy.RawPayload["vendor_data"]!["markers"]!.AsArray().Count, Is.EqualTo(2));
    }

    [Test]
    public void Success_ContainsTheCalculatedPosition()
    {
        RulePosition position = KnownPosition();

        RulePositioningResult result = new RulePositioningSuccess(position);

        Assert.That(result, Is.TypeOf<RulePositioningSuccess>());
        Assert.That(((RulePositioningSuccess)result).Position, Is.SameAs(position));
    }

    [Test]
    public void Success_RejectsNullPosition()
    {
        Assert.Throws<ArgumentNullException>(() => new RulePositioningSuccess(null!));
    }

    [TestCase(RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulePositioningFailureCategory.UnsupportedTask)]
    [TestCase(RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(RulePositioningFailureCategory.PositioningMethodNotImplemented)]
    [TestCase(RulePositioningFailureCategory.StrategyRegistrationAmbiguous)]
    [TestCase(RulePositioningFailureCategory.CalculationFailed)]
    public void Failure_ExposesStableCategoryAndSafeDiagnostic(
        RulePositioningFailureCategory category)
    {
        RulePositioningResult result = new RulePositioningFailure(category);

        Assert.That(result, Is.TypeOf<RulePositioningFailure>());
        RulePositioningFailure failure = (RulePositioningFailure)result;
        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(category));
            Assert.That(failure.Diagnostic, Is.Not.Empty);
            Assert.That(failure.Diagnostic, Does.Not.Contain(kSensitiveCredential));
            Assert.That(failure.Diagnostic, Does.Not.Contain(kCompleteRuleContent));
        });
    }

    [Test]
    public void Context_ContainsExactlyTheCallerResolvedInputs()
    {
        WfImplTask implementationTask = new();
        WfReqTask requestTask = new();
        Device gateway = new();
        Management management = new();
        GatewayProvisioningSettings settings = new();

        RulePositioningContext context = new(
            implementationTask,
            requestTask,
            gateway,
            management,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(context.ImplementationTask, Is.SameAs(implementationTask));
            Assert.That(context.ParentRequestTask, Is.SameAs(requestTask));
            Assert.That(context.TargetGateway, Is.SameAs(gateway));
            Assert.That(context.TargetManagement, Is.SameAs(management));
            Assert.That(context.EffectiveSettings, Is.SameAs(settings));
        });
    }

    /// <summary>
    /// Creates the test-only known position used to prove polymorphic behavior.
    /// </summary>
    private static RulePosition KnownPosition()
    {
        return new MutableTestRulePosition
        {
            SchemaVersion = 4,
            PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            Markers = ["alpha", "beta"]
        };
    }

    /// <summary>
    /// Verifies the common metadata and mutable test-only payload of a known position.
    /// </summary>
    private static void AssertKnownPosition(RulePosition? position)
    {
        Assert.That(position, Is.TypeOf<MutableTestRulePosition>());
        MutableTestRulePosition knownPosition = (MutableTestRulePosition)position!;
        Assert.Multiple(() =>
        {
            Assert.That(knownPosition.PositionType, Is.EqualTo(kKnownDiscriminator));
            Assert.That(knownPosition.SchemaVersion, Is.EqualTo(4));
            Assert.That(
                knownPosition.PositioningAlgorithm,
                Is.EqualTo(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection));
            Assert.That(knownPosition.Markers, Is.EqualTo(new List<string> { "alpha", "beta" }));
        });
    }

    /// <summary>
    /// Verifies that an unknown subtype remains usable through its common contract.
    /// </summary>
    private static void AssertUnknownPosition(RulePosition? position)
    {
        Assert.That(position, Is.TypeOf<UnknownRulePosition>());
        UnknownRulePosition unknownPosition = (UnknownRulePosition)position!;
        Assert.Multiple(() =>
        {
            Assert.That(unknownPosition.PositionType, Is.EqualTo(kUnknownDiscriminator));
            Assert.That(unknownPosition.SchemaVersion, Is.EqualTo(7));
            Assert.That(
                unknownPosition.PositioningAlgorithm,
                Is.EqualTo(ProvisioningPositioningAlgorithm.FortinetEndOfZone));
        });
    }

    /// <summary>
    /// Verifies that vendor-specific fields survive unknown-position serialization.
    /// </summary>
    private static void AssertUnknownContent(JsonObject payload)
    {
        Assert.Multiple(() =>
        {
            Assert.That(payload["position_type"]!.GetValue<string>(), Is.EqualTo(kUnknownDiscriminator));
            Assert.That(payload["schema_version"]!.GetValue<int>(), Is.EqualTo(7));
            Assert.That(payload["positioning_algorithm"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(payload["vendor_data"]!["section"]!.GetValue<string>(), Is.EqualTo("Future section"));
            Assert.That(payload["vendor_data"]!["markers"]!.AsArray().Count, Is.EqualTo(2));
            Assert.That(payload["future_flag"]!.GetValue<bool>(), Is.True);
        });
    }

    /// <summary>
    /// Verifies that persisted type selection does not depend on runtime type metadata.
    /// </summary>
    private static void AssertStableDiscriminator(string json)
    {
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain($"\"position_type\":\"{kKnownDiscriminator}\""));
            Assert.That(json, Does.Not.Contain("$type"));
            Assert.That(json, Does.Not.Contain(nameof(MutableTestRulePosition)));
            Assert.That(json, Does.Not.Contain("FWO.Test"));
        });
    }

    private sealed class PositionEnvelope
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("position"), JsonPropertyName("position")]
        public RulePosition? Position { get; set; }
    }

    [RulePositionType(kKnownDiscriminator)]
    private sealed class MutableTestRulePosition : RulePosition
    {
        [JsonProperty("markers"), JsonPropertyName("markers")]
        public List<string> Markers { get; set; } = [];
    }
}
