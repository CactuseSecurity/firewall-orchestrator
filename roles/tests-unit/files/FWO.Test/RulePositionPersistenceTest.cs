using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using GraphQL;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class RulePositionPersistenceTest
{
    private const string kKnownDiscriminator = "test.persisted-position.v1";
    private const string kUnknownDiscriminator = "future.persisted-position.v8";
    private const string kUnknownTaskJson = """
        {
          "id": 42,
          "rule_position": {
            "position_type": "future.persisted-position.v8",
            "schema_version": 8,
            "positioning_algorithm": 2,
            "vendor_data": {
              "anchor": "future-anchor",
              "markers": ["one", "two"]
            }
          }
        }
        """;
    private const string kReportedUppercasePositionJson = """
        {
          "position_type": "check-point.rule-position.v1",
          "schema_version": 1,
          "positioning_algorithm": "DEFAULT_END_OF_RULEBASE",
          "layer_uid": "network-layer-uid",
          "anchor_kind": "BELOW",
          "anchor_rule_uid": "anchor-rule-uid"
        }
        """;
    private static readonly System.Text.Json.JsonSerializerOptions kGraphQlSystemTextJsonOptions =
        new()
        {
            Converters = { new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseUpper) }
        };

    [Test]
    public void WfImplTask_SystemTextJsonRoundTripsKnownPosition()
    {
        WfImplTask task = new() { Id = 42, RulePosition = KnownPosition("first") };

        string json = System.Text.Json.JsonSerializer.Serialize(task);
        WfImplTask? loaded = System.Text.Json.JsonSerializer.Deserialize<WfImplTask>(json);

        AssertKnownPosition(loaded?.RulePosition, "first");
    }

    [Test]
    public void WfImplTask_NewtonsoftJsonRoundTripsKnownPosition()
    {
        WfImplTask task = new() { Id = 42, RulePosition = KnownPosition("first") };

        string json = JsonConvert.SerializeObject(task);
        WfImplTask? loaded = JsonConvert.DeserializeObject<WfImplTask>(json);

        AssertKnownPosition(loaded?.RulePosition, "first");
    }

    [TestCaseSource(nameof(SupportedAlgorithmRepresentations))]
    public void PositioningAlgorithm_ReadersAcceptPersistedRepresentations(
        string serializedValue,
        ProvisioningPositioningAlgorithm expected)
    {
        string json = $$"""
            {
              "position_type": "{{kKnownDiscriminator}}",
              "schema_version": 3,
              "positioning_algorithm": {{serializedValue}},
              "markers": ["representation"]
            }
            """;

        PersistedTestRulePosition newtonsoftPosition =
            (PersistedTestRulePosition)JsonConvert.DeserializeObject<RulePosition>(json)!;
        PersistedTestRulePosition systemTextPosition =
            (PersistedTestRulePosition)System.Text.Json.JsonSerializer.Deserialize<RulePosition>(json)!;

        Assert.Multiple(() =>
        {
            Assert.That(newtonsoftPosition.PositioningAlgorithm, Is.EqualTo(expected));
            Assert.That(systemTextPosition.PositioningAlgorithm, Is.EqualTo(expected));
        });
    }

    [Test]
    public void ReportedUppercasePosition_LoadsAndRewritesWithoutDataLoss()
    {
        List<RulePosition> loadedPositions =
        [
            JsonConvert.DeserializeObject<RulePosition>(kReportedUppercasePositionJson)!,
            System.Text.Json.JsonSerializer.Deserialize<RulePosition>(
                kReportedUppercasePositionJson,
                kGraphQlSystemTextJsonOptions)!
        ];

        foreach (RulePosition loadedPosition in loadedPositions)
        {
            AssertCheckPointPosition(loadedPosition, ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);
            List<string> rewrittenPayloads =
            [
                JsonConvert.SerializeObject(loadedPosition),
                System.Text.Json.JsonSerializer.Serialize(loadedPosition)
            ];
            foreach (string rewrittenPayload in rewrittenPayloads)
            {
                Assert.That(JsonNode.Parse(rewrittenPayload)!["positioning_algorithm"]!.GetValue<int>(),
                    Is.EqualTo((int)ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
                AssertCheckPointPosition(
                    JsonConvert.DeserializeObject<RulePosition>(rewrittenPayload),
                    ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);
                AssertCheckPointPosition(
                    System.Text.Json.JsonSerializer.Deserialize<RulePosition>(rewrittenPayload),
                    ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);
            }
        }
    }

    [TestCase("\"NOT_AN_ALGORITHM\"")]
    [TestCase("999")]
    [TestCase("2147483648")]
    [TestCase("true")]
    public void KnownPosition_InvalidAlgorithmIsRetainedAsUnknown(string serializedValue)
    {
        string json = CreateCheckPointPositionJson(serializedValue, "3");

        RulePosition? newtonsoftPosition = JsonConvert.DeserializeObject<RulePosition>(json);
        RulePosition? systemTextPosition = System.Text.Json.JsonSerializer.Deserialize<RulePosition>(json);

        Assert.Multiple(() =>
        {
            Assert.That(newtonsoftPosition, Is.TypeOf<UnknownRulePosition>());
            Assert.That(systemTextPosition, Is.TypeOf<UnknownRulePosition>());
            Assert.That(newtonsoftPosition!.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
            Assert.That(systemTextPosition!.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
        });
    }

    [Test]
    public void TicketResponse_MalformedPositionDoesNotPreventOtherTasksLoading()
    {
        string json = $$"""
            [
              { "id": 41, "rule_position": {{CreateCheckPointPositionJson("999", "3")}} },
              { "id": 42, "rule_position": {{CreateCheckPointPositionJson("5", "3")}} }
            ]
            """;

        List<WfImplTask> newtonsoftTasks = JsonConvert.DeserializeObject<List<WfImplTask>>(json)!;
        List<WfImplTask> systemTextTasks = System.Text.Json.JsonSerializer.Deserialize<List<WfImplTask>>(json)!;

        Assert.Multiple(() =>
        {
            Assert.That(newtonsoftTasks, Has.Count.EqualTo(2));
            Assert.That(systemTextTasks, Has.Count.EqualTo(2));
            Assert.That(newtonsoftTasks[0].RulePosition, Is.TypeOf<UnknownRulePosition>());
            Assert.That(systemTextTasks[0].RulePosition, Is.TypeOf<UnknownRulePosition>());
            AssertCheckPointPosition(newtonsoftTasks[1].RulePosition,
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "layer", "anchor");
            AssertCheckPointPosition(systemTextTasks[1].RulePosition,
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "layer", "anchor");
        });
    }

    [TestCase("\"NOT_AN_ALGORITHM\"")]
    [TestCase("999")]
    [TestCase("2147483648")]
    [TestCase("true")]
    public void UnknownPosition_InvalidAlgorithmRetainsRawPayload(string serializedValue)
    {
        string json = $$"""
            {
              "position_type": "{{kUnknownDiscriminator}}",
              "schema_version": 8,
              "positioning_algorithm": {{serializedValue}},
              "vendor_data": { "anchor": "retained" }
            }
            """;

        UnknownRulePosition newtonsoftPosition =
            (UnknownRulePosition)JsonConvert.DeserializeObject<RulePosition>(json)!;
        UnknownRulePosition systemTextPosition =
            (UnknownRulePosition)System.Text.Json.JsonSerializer.Deserialize<RulePosition>(json)!;

        Assert.Multiple(() =>
        {
            Assert.That(newtonsoftPosition.RawPayload["vendor_data"]!["anchor"]!.GetValue<string>(),
                Is.EqualTo("retained"));
            Assert.That(systemTextPosition.RawPayload["vendor_data"]!["anchor"]!.GetValue<string>(),
                Is.EqualTo("retained"));
        });
    }

    [Test]
    public void WfImplTask_SystemTextJsonPreservesUnknownPosition()
    {
        WfImplTask task = System.Text.Json.JsonSerializer.Deserialize<WfImplTask>(kUnknownTaskJson)!;

        string json = System.Text.Json.JsonSerializer.Serialize(task);

        AssertUnknownPosition(task.RulePosition, json);
    }

    [Test]
    public void WfImplTask_NewtonsoftJsonPreservesUnknownPosition()
    {
        WfImplTask task = JsonConvert.DeserializeObject<WfImplTask>(kUnknownTaskJson)!;

        string json = JsonConvert.SerializeObject(task);

        AssertUnknownPosition(task.RulePosition, json);
    }

    [TestCase("{\"id\":42}")]
    [TestCase("{\"id\":42,\"rule_position\":null}")]
    public void WfImplTask_MissingOrNullPositionLoadsAsNull(string json)
    {
        WfImplTask? systemTextTask = System.Text.Json.JsonSerializer.Deserialize<WfImplTask>(json);
        WfImplTask? newtonsoftTask = JsonConvert.DeserializeObject<WfImplTask>(json);

        Assert.Multiple(() =>
        {
            Assert.That(systemTextTask?.RulePosition, Is.Null);
            Assert.That(newtonsoftTask?.RulePosition, Is.Null);
        });
    }

    [Test]
    public void WfImplTaskCopy_DeepCopiesKnownPosition()
    {
        PersistedTestRulePosition originalPosition = KnownPosition("first");
        WfImplTask original = new() { Id = 42, RulePosition = originalPosition };

        WfImplTask copy = new(original);
        originalPosition.Markers.Add("changed-after-copy");

        AssertKnownPosition(copy.RulePosition, "first");
        Assert.That(copy.RulePosition, Is.Not.SameAs(original.RulePosition));
    }

    [Test]
    public void WfImplTaskCopy_DeepCopiesUnknownPosition()
    {
        WfImplTask original = System.Text.Json.JsonSerializer.Deserialize<WfImplTask>(kUnknownTaskJson)!;

        WfImplTask copy = new(original);
        UnknownRulePosition originalPosition = (UnknownRulePosition)original.RulePosition!;
        originalPosition.RawPayload["vendor_data"]!["markers"]!.AsArray().Add("changed-after-copy");

        UnknownRulePosition copiedPosition = (UnknownRulePosition)copy.RulePosition!;
        Assert.That(copiedPosition.RawPayload["vendor_data"]!["markers"]!.AsArray().Count, Is.EqualTo(2));
    }

    [Test]
    public async Task DedicatedOperation_StoresReplacesAndClearsOnlyRulePosition()
    {
        RecordingApiConnection apiConnection = new() { UpdatedId = 42 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);
        PersistedTestRulePosition first = KnownPosition("first");
        PersistedTestRulePosition replacement = KnownPosition("replacement");

        bool stored = await dbAccess.UpdateImplTaskRulePositionInDb(42, first);
        bool replaced = await dbAccess.UpdateImplTaskRulePositionInDb(42, replacement);
        bool cleared = await dbAccess.UpdateImplTaskRulePositionInDb(42, null);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.True);
            Assert.That(replaced, Is.True);
            Assert.That(cleared, Is.True);
            Assert.That(apiConnection.Calls, Has.Count.EqualTo(3));
            Assert.That(apiConnection.Calls.All(call => call.Query == RequestQueries.updateImplementationTaskRulePosition), Is.True);
        });
        AssertVariables(apiConnection.Calls[0].Variables, 42, first);
        AssertVariables(apiConnection.Calls[1].Variables, 42, replacement);
        AssertVariables(apiConnection.Calls[2].Variables, 42, null);
    }

    [Test]
    public async Task DedicatedOperation_ReturnsFalseForUnexpectedUpdatedTask()
    {
        RecordingApiConnection apiConnection = new() { UpdatedId = 99 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        bool stored = await dbAccess.UpdateImplTaskRulePositionInDb(42, KnownPosition("first"));

        Assert.That(stored, Is.False);
        Assert.That(apiConnection.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DedicatedOperation_ReturnsFalseWhenPersistenceFails()
    {
        RecordingApiConnection apiConnection = new() { ThrowOnUpdate = true };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        bool stored = await dbAccess.UpdateImplTaskRulePositionInDb(42, KnownPosition("first"));

        Assert.That(stored, Is.False);
        Assert.That(apiConnection.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public void DedicatedMutation_UpdatesOnlyRulePosition()
    {
        string mutation = RequestQueries.updateImplementationTaskRulePosition;

        Assert.Multiple(() =>
        {
            Assert.That(mutation, Does.Contain("$id: bigint!"));
            Assert.That(mutation, Does.Contain("$rulePosition: jsonb"));
            Assert.That(mutation, Does.Contain("_set: { rule_position: $rulePosition }"));
            Assert.That(mutation, Does.Not.Contain("title:"));
            Assert.That(mutation, Does.Not.Contain("state_id:"));
            Assert.That(mutation, Does.Not.Contain("implementation_action:"));
        });
    }

    [Test]
    public void GeneralMutations_DoNotWriteRulePosition()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RequestQueries.newImplementationTask, Does.Not.Contain("rule_position"));
            Assert.That(RequestQueries.newImplementationTask, Does.Not.Contain("$rulePosition"));
            Assert.That(RequestQueries.updateImplementationTask, Does.Not.Contain("rule_position"));
            Assert.That(RequestQueries.updateImplementationTask, Does.Not.Contain("$rulePosition"));
        });
    }

    [Test]
    public void DedicatedMutation_TransportSerializesKnownAndUnknownPositions()
    {
        WfImplTask unknownTask = JsonConvert.DeserializeObject<WfImplTask>(kUnknownTaskJson)!;
        List<string> knownPayloads = SerializeGraphQlVariables(KnownPosition("transport"));
        List<string> unknownPayloads = SerializeGraphQlVariables(unknownTask.RulePosition);

        Assert.Multiple(() =>
        {
            Assert.That(knownPayloads.All(payload => payload.Contains(kKnownDiscriminator)), Is.True);
            Assert.That(knownPayloads.All(payload => payload.Contains("transport")), Is.True);
            Assert.That(unknownPayloads.All(payload => payload.Contains(kUnknownDiscriminator)), Is.True);
            Assert.That(unknownPayloads.All(payload => payload.Contains("future-anchor")), Is.True);
        });
    }

    [TestCaseSource(nameof(DefinedPositioningAlgorithms))]
    public void GraphQlSerializers_RoundTripEveryDefinedAlgorithmNumerically(
        ProvisioningPositioningAlgorithm algorithm)
    {
        CheckPointRulePosition position = new(
            "transport-layer",
            CheckPointPositionAnchorKind.Below,
            "transport-anchor",
            algorithm)
        {
            SchemaVersion = 4
        };

        foreach (string requestJson in SerializeGraphQlVariables(position))
        {
            JsonObject transportedPosition = JsonNode.Parse(requestJson)!["variables"]!["rulePosition"]!.AsObject();
            Assert.That(transportedPosition["positioning_algorithm"]!.GetValue<int>(),
                Is.EqualTo((int)algorithm));
            string positionJson = transportedPosition.ToJsonString();

            AssertCheckPointPosition(JsonConvert.DeserializeObject<RulePosition>(positionJson), algorithm,
                "transport-layer", "transport-anchor", 4);
            AssertCheckPointPosition(
                System.Text.Json.JsonSerializer.Deserialize<RulePosition>(
                    positionJson,
                    kGraphQlSystemTextJsonOptions),
                algorithm,
                "transport-layer",
                "transport-anchor",
                4);
        }
    }

    private static PersistedTestRulePosition KnownPosition(string marker)
    {
        return new PersistedTestRulePosition
        {
            SchemaVersion = 3,
            PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            Markers = [marker]
        };
    }

    private static IEnumerable<TestCaseData> SupportedAlgorithmRepresentations()
    {
        foreach (ProvisioningPositioningAlgorithm algorithm in
            Enum.GetValues<ProvisioningPositioningAlgorithm>())
        {
            int numericValue = (int)algorithm;
            yield return new TestCaseData(numericValue.ToString(), algorithm)
                .SetName($"Algorithm integer {algorithm}");
            yield return new TestCaseData($"\"{numericValue}\"", algorithm)
                .SetName($"Algorithm numeric string {algorithm}");
            yield return new TestCaseData($"\"{algorithm}\"", algorithm)
                .SetName($"Algorithm C# name {algorithm}");
            yield return new TestCaseData($"\"{ToScreamingSnakeCase(algorithm.ToString())}\"", algorithm)
                .SetName($"Algorithm GraphQL name {algorithm}");

            string? enumMemberValue = typeof(ProvisioningPositioningAlgorithm)
                .GetField(algorithm.ToString())!
                .GetCustomAttribute<EnumMemberAttribute>()?.Value;
            if (enumMemberValue != null)
            {
                yield return new TestCaseData($"\"{enumMemberValue}\"", algorithm)
                    .SetName($"Algorithm EnumMember value {algorithm}");
            }
        }
    }

    private static IEnumerable<ProvisioningPositioningAlgorithm> DefinedPositioningAlgorithms() =>
        Enum.GetValues<ProvisioningPositioningAlgorithm>()
            .Where(algorithm => algorithm != ProvisioningPositioningAlgorithm.Undefined);

    private static string ToScreamingSnakeCase(string value)
    {
        return string.Concat(value.SelectMany((character, index) =>
            index > 0 && char.IsUpper(character)
                ? new[] { '_', char.ToUpperInvariant(character) }
                : new[] { char.ToUpperInvariant(character) }));
    }

    private static string CreateCheckPointPositionJson(string algorithm, string anchorKind) => $$"""
        {
          "position_type": "check-point.rule-position.v1",
          "schema_version": 1,
          "positioning_algorithm": {{algorithm}},
          "layer_uid": "layer",
          "anchor_kind": {{anchorKind}},
          "anchor_rule_uid": "anchor"
        }
        """;

    private static void AssertCheckPointPosition(
        RulePosition? position,
        ProvisioningPositioningAlgorithm expectedAlgorithm,
        string expectedLayerUid = "network-layer-uid",
        string expectedAnchorRuleUid = "anchor-rule-uid",
        int expectedSchemaVersion = 1)
    {
        Assert.That(position, Is.TypeOf<CheckPointRulePosition>());
        CheckPointRulePosition checkPointPosition = (CheckPointRulePosition)position!;
        Assert.Multiple(() =>
        {
            Assert.That(checkPointPosition.PositionType, Is.EqualTo(CheckPointRulePosition.kDiscriminator));
            Assert.That(checkPointPosition.SchemaVersion, Is.EqualTo(expectedSchemaVersion));
            Assert.That(checkPointPosition.PositioningAlgorithm, Is.EqualTo(expectedAlgorithm));
            Assert.That(checkPointPosition.LayerUid, Is.EqualTo(expectedLayerUid));
            Assert.That(checkPointPosition.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Below));
            Assert.That(checkPointPosition.AnchorRuleUid, Is.EqualTo(expectedAnchorRuleUid));
        });
    }

    private static void AssertKnownPosition(RulePosition? position, string marker)
    {
        Assert.That(position, Is.TypeOf<PersistedTestRulePosition>());
        PersistedTestRulePosition knownPosition = (PersistedTestRulePosition)position!;
        Assert.Multiple(() =>
        {
            Assert.That(knownPosition.PositionType, Is.EqualTo(kKnownDiscriminator));
            Assert.That(knownPosition.SchemaVersion, Is.EqualTo(3));
            Assert.That(knownPosition.PositioningAlgorithm,
                Is.EqualTo(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection));
            Assert.That(knownPosition.Markers, Is.EqualTo(new List<string> { marker }));
        });
    }

    private static void AssertUnknownPosition(RulePosition? position, string serializedTask)
    {
        Assert.That(position, Is.TypeOf<UnknownRulePosition>());
        Assert.Multiple(() =>
        {
            Assert.That(position!.PositionType, Is.EqualTo(kUnknownDiscriminator));
            Assert.That(position.SchemaVersion, Is.EqualTo(8));
            Assert.That(serializedTask, Does.Contain("future-anchor"));
            Assert.That(serializedTask, Does.Contain("markers"));
        });
    }

    private static WfDbAccess CreateDbAccess(RecordingApiConnection apiConnection)
    {
        WfHandler handler = new();
        ActionHandler actionHandler = new(apiConnection, handler);
        return new WfDbAccess(
            DefaultInit.DoNothing,
            new UserConfig(),
            apiConnection,
            actionHandler,
            false,
            WorkflowPhases.implementation);
    }

    private static void AssertVariables(object? variables, long expectedId, RulePosition? expectedPosition)
    {
        Assert.That(variables, Is.Not.Null);
        PropertyInfo[] properties = variables!.GetType().GetProperties();
        Assert.Multiple(() =>
        {
            Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(new List<string> { "id", "rulePosition" }));
            Assert.That(properties.Single(property => property.Name == "id").GetValue(variables), Is.EqualTo(expectedId));
            Assert.That(properties.Single(property => property.Name == "rulePosition").GetValue(variables), Is.SameAs(expectedPosition));
        });
    }

    private static List<string> SerializeGraphQlVariables(RulePosition? position)
    {
        GraphQLRequest request = new()
        {
            Query = RequestQueries.updateImplementationTaskRulePosition,
            Variables = new { id = 42, rulePosition = position }
        };
        return
        [
            new NewtonsoftJsonSerializer().SerializeToString(request),
            new GraphQL.Client.Serializer.SystemTextJson.SystemTextJsonSerializer().SerializeToString(request)
        ];
    }

    [RulePositionType(kKnownDiscriminator)]
    private sealed class PersistedTestRulePosition : RulePosition
    {
        [JsonProperty("markers"), JsonPropertyName("markers")]
        public List<string> Markers { get; set; } = [];
    }

    private sealed class RecordingApiConnection : SimulatedApiConnection
    {
        public long UpdatedId { get; set; }
        public bool ThrowOnUpdate { get; set; }
        public List<(string Query, object? Variables)> Calls { get; } = [];

        public override Task<T> SendQueryAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Calls.Add((query, variables));
            if (ThrowOnUpdate)
            {
                throw new InvalidOperationException("persistence failed");
            }
            return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedId });
        }
    }
}
