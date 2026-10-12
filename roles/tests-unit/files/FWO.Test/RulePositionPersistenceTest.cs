using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
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

    [Test]
    public void ImplTaskMetadata_PreservesReadAccessAndLimitsNewWriteAccess()
    {
        JsonObject metadata = JsonNode.Parse(File.ReadAllText(LocateRepositoryFile(
            "roles", "api", "files", "replace_metadata.json")))!.AsObject();
        JsonObject table = FindImplTaskMetadata(metadata);
        List<JsonObject> insertPermissions = ReadPermissionObjects(table, "insert_permissions");
        List<JsonObject> selectPermissions = ReadPermissionObjects(table, "select_permissions");
        List<JsonObject> updatePermissions = ReadPermissionObjects(table, "update_permissions");

        Assert.Multiple(() =>
        {
            Assert.That(insertPermissions.All(HasRulePositionColumn), Is.True);
            Assert.That(selectPermissions.All(HasRulePositionColumn), Is.True);
            Assert.That(updatePermissions.Where(HasRulePositionColumn).Select(ReadRole),
                Is.EquivalentTo(new List<string> { "middleware-server", "planner" }));
        });
    }

    [Test]
    public void UpgradeAndCreationSchema_DeclareNullableIdempotentJsonbColumn()
    {
        string upgrade = File.ReadAllText(LocateRepositoryFile(
            "roles", "database", "files", "upgrade", "9.7.1.sql"));
        string creation = File.ReadAllText(LocateRepositoryFile(
            "roles", "database", "files", "sql", "creation", "fworch-create-tables-request.sql"));

        Assert.Multiple(() =>
        {
            Assert.That(upgrade, Does.Contain("ADD COLUMN IF NOT EXISTS rule_position jsonb"));
            Assert.That(upgrade, Does.Not.Contain("rule_position jsonb NOT NULL"));
            Assert.That(creation, Does.Contain("rule_position jsonb"));
            Assert.That(creation, Does.Not.Contain("rule_position jsonb NOT NULL"));
        });
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

    private static JsonObject FindImplTaskMetadata(JsonObject metadata)
    {
        JsonArray sources = metadata["args"]!["metadata"]!["sources"]!.AsArray();
        return sources.SelectMany(source => source!["tables"]!.AsArray())
            .Select(table => table!.AsObject())
            .Single(table => table["table"]!["schema"]!.GetValue<string>() == "request"
                && table["table"]!["name"]!.GetValue<string>() == "impltask");
    }

    private static List<JsonObject> ReadPermissionObjects(JsonObject table, string permissionName)
    {
        return table[permissionName]!.AsArray().Select(permission => permission!.AsObject()).ToList();
    }

    private static bool HasRulePositionColumn(JsonObject permission)
    {
        return permission["permission"]!["columns"]!.AsArray()
            .Any(column => column!.GetValue<string>() == "rule_position");
    }

    private static string ReadRole(JsonObject permission)
    {
        return permission["role"]!.GetValue<string>();
    }

    private static string LocateRepositoryFile(params string[] relativePath)
    {
        foreach (string startPath in new List<string> { TestContext.CurrentContext.TestDirectory, Environment.CurrentDirectory })
        {
            for (DirectoryInfo? directory = new(startPath); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine([directory.FullName, .. relativePath]);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        throw new FileNotFoundException($"Repository file not found: {Path.Combine(relativePath)}");
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
