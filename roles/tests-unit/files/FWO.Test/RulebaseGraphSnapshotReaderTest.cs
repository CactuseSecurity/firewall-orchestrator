using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class RulebaseGraphSnapshotReaderTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const long kImportId = 100;
    private const string kSentinel = "SECRET exception rule-name uid-content";

    private const string kImportJson = """
        [{
          "control_id": 100,
          "mgm_id": 10,
          "import_type_id": 1,
          "successful_import": true,
          "stop_time": "2026-10-15T12:00:00Z"
        }]
        """;

    private const string kLinksJson = """
        [{
          "dev_id": 20,
          "mgm_id": 10,
          "rulebase_links": [
            { "id": 1, "gw_id": 20, "from_rule_id": null, "from_rulebase_id": null,
              "to_rulebase_id": 1, "link_type": 1, "is_initial": true, "is_global": false,
              "is_section": false, "created": 90, "removed": null, "rule": null },
            { "id": 2, "gw_id": 20, "from_rule_id": null, "from_rulebase_id": 1,
              "to_rulebase_id": 2, "link_type": 2, "is_initial": false, "is_global": true,
              "is_section": false, "created": 90, "removed": 101, "rule": null },
            { "id": 3, "gw_id": 20, "from_rule_id": null, "from_rulebase_id": 2,
              "to_rulebase_id": 3, "link_type": 3, "is_initial": false, "is_global": false,
              "is_section": true, "created": 100, "removed": null, "rule": null },
            { "id": 4, "gw_id": 20, "from_rule_id": 201, "from_rulebase_id": 2,
              "to_rulebase_id": 4, "link_type": 4, "is_initial": false, "is_global": false,
              "is_section": false, "created": 95, "removed": null,
              "rule": { "rule_id": 201, "mgm_id": 10, "rulebase_id": 2,
                        "rule_create": 95, "removed": null } }
          ]
        }]
        """;

    private const string kRulebasesJson = """
        [
          { "id": 1, "name": "Policy", "uid": "rb-1", "mgm_id": 10,
            "created": 80, "removed": null, "rules": [] },
          { "id": 2, "name": "Domain", "uid": "rb-2", "mgm_id": 10,
            "created": 80, "removed": null, "rules": [
              { "rule_id": 202, "rule_uid": "rule-202", "mgm_id": 10, "rulebase_id": 2,
                "rule_num_numeric": 2.0, "rule_create": 99, "removed": null },
              { "rule_id": 201, "rule_uid": "rule-201", "mgm_id": 10, "rulebase_id": 2,
                "rule_num_numeric": 1.0, "rule_create": 95, "removed": 105 }
            ] },
          { "id": 3, "name": "Section", "uid": "rb-3", "mgm_id": 10,
            "created": 100, "removed": null, "rules": [] },
          { "id": 4, "name": "Empty inline", "uid": "rb-4", "mgm_id": 10,
            "created": 90, "removed": null, "rules": [] }
        ]
        """;

    [Test]
    public async Task ReadAsync_ReturnsImmutableRepresentativeSnapshotAndUsesExactScope()
    {
        SimulatedApiConnection api = SuccessfulApi();
        RulebaseGraphSnapshotReader reader = new(api);

        RulebaseGraphReadSuccess result = (RulebaseGraphReadSuccess)await reader.ReadAsync(
            kManagementId, kGatewayId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Snapshot.ManagementId, Is.EqualTo(kManagementId));
            Assert.That(result.Snapshot.GatewayId, Is.EqualTo(kGatewayId));
            Assert.That(result.Snapshot.Rulebases.Select(rulebase => rulebase.Id), Is.EqualTo(new long[] { 1, 2, 3, 4 }));
            Assert.That(result.Snapshot.Rulebases.Single(rulebase => rulebase.Id == 2).Rules.Select(rule => rule.Id),
                Is.EqualTo(new long[] { 201, 202 }));
            Assert.That(result.Snapshot.Links, Has.Count.EqualTo(4));
            Assert.That(result.Snapshot.Links.Any(link => link.IsInitial), Is.True);
            Assert.That(result.Snapshot.Links.Any(link => link.IsGlobal), Is.True);
            Assert.That(result.Snapshot.Links.Any(link => link.IsSection), Is.True);
            Assert.That(result.Snapshot.Links.Any(link => link.SourceRuleId == 201), Is.True);
            Assert.That(result.Snapshot.Rulebases.Single(rulebase => rulebase.Id == 4).Rules, Is.Empty);
        });

        AssertExactCalls(api);
    }

    [Test]
    public async Task ImportedEmptyInitialLayer_ProducesGenericBottomPosition()
    {
        SimulatedApiConnection api = EmptyInitialLayerApi();
        Management management = new()
        {
            Id = kManagementId,
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" }
        };
        Device gateway = new()
        {
            Id = kGatewayId,
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" },
            Management = management
        };
        WfReqTask requestTask = new()
        {
            Id = 30,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ManagementId = kManagementId,
            OnManagement = management
        };
        requestTask.SetDeviceList([kGatewayId]);
        RulePositioningContext context = new(
            new WfImplTask
            {
                Id = 40,
                ReqTaskId = requestTask.Id,
                DeviceId = kGatewayId,
                TaskType = WfTaskType.access.ToString(),
                ImplAction = RequestAction.create.ToString()
            },
            requestTask,
            gateway,
            management,
            new GatewayDeviceSpecificSettings
            {
                PositioningAlgorithm = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase
            });
        CheckPointGraphRulePositioningStrategy strategy = new(
            new RulebaseGraphSnapshotReader(api),
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(context, CancellationToken.None);

        CheckPointRulePosition position = (CheckPointRulePosition)((RulePositioningSuccess)result).Position;
        Assert.Multiple(() =>
        {
            Assert.That(position.LayerUid, Is.EqualTo("rb-1"));
            Assert.That(position.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Bottom));
            Assert.That(position.AnchorRuleUid, Is.Null);
        });
        AssertExactCalls(api, [1]);
    }

    /// <summary>Creates imported data for one existing, structurally valid empty initial layer.</summary>
    private static SimulatedApiConnection EmptyInitialLayerApi()
    {
        const string linksJson = """
            [{
              "dev_id": 20,
              "mgm_id": 10,
              "rulebase_links": [
                { "id": 1, "gw_id": 20, "from_rule_id": null, "from_rulebase_id": null,
                  "to_rulebase_id": 1, "link_type": 1, "is_initial": true, "is_global": false,
                  "is_section": false, "created": 90, "removed": null, "rule": null }
              ]
            }]
            """;
        const string rulebasesJson = """
            [{
              "id": 1,
              "name": "Empty policy",
              "uid": "rb-1",
              "mgm_id": 10,
              "created": 80,
              "removed": null,
              "rules": []
            }]
            """;
        return new SimulatedApiConnection(kImportJson, linksJson, rulebasesJson);
    }

    [TestCase(0, kGatewayId)]
    [TestCase(kManagementId, 0)]
    [TestCase(-1, kGatewayId)]
    public async Task ReadAsync_InvalidScopeReturnsWithoutCallingApi(int managementId, int gatewayId)
    {
        SimulatedApiConnection api = new(Array.Empty<string>());

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(managementId, gatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.InvalidInput);

        Assert.That(failure.Diagnostic, Is.EqualTo("The rulebase graph scope is invalid."));
        Assert.That(api.Calls, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_NoImportReturnsExplicitFailureWithoutGraphCalls()
    {
        SimulatedApiConnection api = new("[]");

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.NoSuccessfulImport);

        Assert.That(failure.Diagnostic, Is.EqualTo("No successfully completed policy import is available."));
        Assert.That(api.Calls, Has.Count.EqualTo(1));
    }

    [TestCase("[]")]
    [TestCase("[{\"dev_id\":20,\"mgm_id\":99,\"rulebase_links\":[]}]")]
    [TestCase("[{\"dev_id\":20,\"mgm_id\":10,\"rulebase_links\":[]},{\"dev_id\":20,\"mgm_id\":10,\"rulebase_links\":[]}]")]
    public async Task ReadAsync_InvalidGatewayScopeStopsBeforeRulebases(string gatewayJson)
    {
        SimulatedApiConnection api = new(kImportJson, gatewayJson);

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.ScopeMismatch);

        Assert.That(failure.Diagnostic, Is.EqualTo("The requested management and gateway scope is unavailable or inconsistent."));
        Assert.That(api.Calls, Has.Count.EqualTo(2));
    }

    [TestCaseSource(nameof(InvalidImportCases))]
    public async Task ReadAsync_ContradictoryImportIsInvalidGraphData(string importJson)
    {
        SimulatedApiConnection api = new(importJson);

        AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.InvalidGraphData);
        Assert.That(api.Calls, Has.Count.EqualTo(1));
    }

    [TestCaseSource(nameof(InvalidLinkCases))]
    public async Task ReadAsync_InvalidLinksStopBeforeRulebases(string linksJson)
    {
        SimulatedApiConnection api = new(kImportJson, linksJson);

        AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.InvalidGraphData);
        Assert.That(api.Calls, Has.Count.EqualTo(2));
    }

    [TestCaseSource(nameof(InvalidRulebaseCases))]
    public async Task ReadAsync_InvalidEndpointOrSnapshotDataIsRejected(string rulebasesJson)
    {
        SimulatedApiConnection api = new(kImportJson, kLinksJson, rulebasesJson);

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.InvalidGraphData);

        Assert.That(failure.Diagnostic, Is.EqualTo("The imported rulebase graph is incomplete or invalid."));
        Assert.That(api.Calls, Has.Count.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task ReadAsync_QueryFailureAtEveryStageIsSafeAndStops(int failedStage)
    {
        string[] responses = [kImportJson, kLinksJson, kRulebasesJson];
        SimulatedApiConnection api = new(responses.Select((json, index) =>
            index == failedStage ? Response.Failure(new InvalidOperationException(kSentinel)) : Response.FromJson(json)).ToArray());

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.QueryFailed);

        Assert.Multiple(() =>
        {
            Assert.That(failure.Diagnostic, Is.EqualTo("The imported rulebase graph could not be loaded."));
            Assert.That(failure.Diagnostic, Does.Not.Contain(kSentinel));
            Assert.That(api.Calls, Has.Count.EqualTo(failedStage + 1));
        });
    }

    [Test]
    public async Task ReadAsync_DeserializationFailureReturnsSafeQueryFailure()
    {
        SimulatedApiConnection api = new("not-json and includes fixture contents");

        RulebaseGraphReadFailure failure = AssertFailure(
            await new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, CancellationToken.None),
            RulebaseGraphReadFailureCategory.QueryFailed);

        Assert.That(failure.Diagnostic, Is.EqualTo("The imported rulebase graph could not be loaded."));
        Assert.That(failure.Diagnostic, Does.Not.Contain("fixture contents"));
    }

    [Test]
    public void ReadAsync_PreCancellationPropagatesWithoutCallingApi()
    {
        SimulatedApiConnection api = SuccessfulApi();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            new RulebaseGraphSnapshotReader(api).ReadAsync(kManagementId, kGatewayId, cancellation.Token));
        Assert.That(api.Calls, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task ReadAsync_CancellationWhileAwaitingEveryStagePropagatesAndStops(int cancelledStage)
    {
        string[] responses = [kImportJson, kLinksJson, kRulebasesJson];
        SimulatedApiConnection api = new(responses.Select((json, index) =>
            index == cancelledStage ? Response.AwaitCancellation() : Response.FromJson(json)).ToArray());
        using CancellationTokenSource cancellation = new();
        Task<RulebaseGraphReadResult> read = new RulebaseGraphSnapshotReader(api).ReadAsync(
            kManagementId, kGatewayId, cancellation.Token);
        await api.WaitUntilAwaitingAsync();

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await read);
        Assert.That(api.Calls, Has.Count.EqualTo(cancelledStage + 1));
    }

    [Test]
    public void ResultContractsRejectNullAndUseSafeFallbackDiagnostic()
    {
        Assert.Throws<ArgumentNullException>(() => new RulebaseGraphReadSuccess(null!));
        RulebaseGraphReadFailure failure = new((RulebaseGraphReadFailureCategory)999);
        Assert.That(failure.Diagnostic, Is.EqualTo("The imported rulebase graph could not be loaded."));
    }

    private static IEnumerable<string> InvalidImportCases()
    {
        yield return "[null]";
        yield return $"[{kImportJson.Trim('[', ']')},{kImportJson.Trim('[', ']')}]";
        yield return Replace(kImportJson, "control_id", 0);
        yield return Replace(kImportJson, "mgm_id", 99);
        yield return Replace(kImportJson, "import_type_id", 2);
        yield return Replace(kImportJson, "successful_import", false);
        yield return Replace(kImportJson, "stop_time", null);
    }

    private static IEnumerable<string> InvalidLinkCases()
    {
        yield return Replace(kLinksJson, "rulebase_links", new JArray());
        yield return Mutate(kLinksJson, root => ((JArray)root[0]!["rulebase_links"]!).Add(JValue.CreateNull()));
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![1]!["id"] = 1);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![0]!["to_rulebase_id"] = null);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![0]!["created"] = 101);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![0]!["removed"] = 100);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![3]!["rule"] = null);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![3]!["rule"]!["mgm_id"] = 99);
        yield return Mutate(kLinksJson, root => root[0]!["rulebase_links"]![3]!["rule"]!["rulebase_id"] = 3);
    }

    private static IEnumerable<string> InvalidRulebaseCases()
    {
        yield return "[]";
        yield return Mutate(kRulebasesJson, root => root.Add(JValue.CreateNull()));
        yield return Mutate(kRulebasesJson, root => root[3]!.Remove());
        yield return Mutate(kRulebasesJson, root => root[3]!["id"] = 5);
        yield return Mutate(kRulebasesJson, root => root[3]!["id"] = 3);
        yield return Mutate(kRulebasesJson, root => root[0]!["mgm_id"] = 99);
        yield return Mutate(kRulebasesJson, root => root[0]!["created"] = 101);
        yield return Mutate(kRulebasesJson, root => root[1]!["rules"]![0]!["rulebase_id"] = 1);
        yield return Mutate(kRulebasesJson, root => root[1]!["rules"]![0]!["rule_create"] = 101);
        yield return Mutate(kRulebasesJson, root => ((JArray)root[1]!["rules"]!).Add(JValue.CreateNull()));
        yield return Mutate(kRulebasesJson, root => root[1]!["rules"]![0]!["rule_id"] = 201);
        yield return Mutate(kRulebasesJson, root => root[1]!["rules"]![0]!["rule_uid"] = "rule-201");
        yield return Mutate(kRulebasesJson, root => root[1]!["rules"]![0]!["rule_num_numeric"] = 1.0);
        yield return Mutate(kRulebasesJson, root => root[0]!["uid"] = " ");
        yield return Mutate(kRulebasesJson, root => ((JArray)root[1]!["rules"]!).RemoveAt(1));
    }

    private static SimulatedApiConnection SuccessfulApi() => new(kImportJson, kLinksJson, kRulebasesJson);

    private static RulebaseGraphReadFailure AssertFailure(
        RulebaseGraphReadResult result,
        RulebaseGraphReadFailureCategory category)
    {
        Assert.That(result, Is.TypeOf<RulebaseGraphReadFailure>());
        RulebaseGraphReadFailure failure = (RulebaseGraphReadFailure)result;
        Assert.That(failure.Category, Is.EqualTo(category));
        return failure;
    }

    private static void AssertExactCalls(
        SimulatedApiConnection api,
        IReadOnlyList<int>? expectedRulebaseIds = null)
    {
        Assert.That(api.Calls.Select(call => call.OperationName), Is.EqualTo(new[]
        {
            "getLatestSuccessfulRuleImport",
            "getGatewayRulebaseLinksAtImport",
            "getRulebasesAtImport"
        }));
        Assert.That(api.Calls.Select(call => call.Query), Is.EqualTo(new[]
        {
            RulePositioningQueries.getLatestSuccessfulRuleImport,
            RulePositioningQueries.getGatewayRulebaseLinksAtImport,
            RulePositioningQueries.getRulebasesAtImport
        }));

        JObject importVariables = JObject.FromObject(api.Calls[0].Variables!);
        JObject linkVariables = JObject.FromObject(api.Calls[1].Variables!);
        JObject rulebaseVariables = JObject.FromObject(api.Calls[2].Variables!);
        Assert.Multiple(() =>
        {
            Assert.That((int?)importVariables["managementId"], Is.EqualTo(kManagementId));
            Assert.That((int?)importVariables["importTypeId"], Is.EqualTo(1));
            Assert.That((long?)linkVariables["importId"], Is.EqualTo(kImportId));
            Assert.That((int?)linkVariables["gatewayId"], Is.EqualTo(kGatewayId));
            Assert.That((long?)rulebaseVariables["importId"], Is.EqualTo(kImportId));
            Assert.That((int?)rulebaseVariables["managementId"], Is.EqualTo(kManagementId));
            Assert.That(rulebaseVariables["rulebaseIds"]!.Values<int>(),
                Is.EqualTo(expectedRulebaseIds ?? new[] { 1, 2, 3, 4 }));
        });
    }

    private static string Replace(string json, string propertyName, object? value) =>
        Mutate(json, root => root[0]![propertyName] = value == null ? JValue.CreateNull() : JToken.FromObject(value));

    private static string Mutate(string json, Action<JArray> mutation)
    {
        JArray root = JArray.Parse(json);
        mutation(root);
        return root.ToString(Formatting.None);
    }

    private sealed record ApiCall(string Query, object? Variables, string? OperationName);

    private sealed record Response(string? Json, Exception? Exception, bool Waits)
    {
        internal static Response FromJson(string json) => new(json, null, false);
        internal static Response Failure(Exception exception) => new(null, exception, false);
        internal static Response AwaitCancellation() => new(null, null, true);
    }

    private sealed class SimulatedApiConnection : ApiConnection
    {
        private readonly Queue<Response> responses;
        private readonly TaskCompletionSource awaiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<ApiCall> Calls { get; } = [];

        internal SimulatedApiConnection(params string[] jsonResponses)
            : this(jsonResponses.Select(Response.FromJson).ToArray())
        {
        }

        internal SimulatedApiConnection(params Response[] configuredResponses)
        {
            responses = new Queue<Response>(configuredResponses);
        }

        internal Task WaitUntilAwaitingAsync() => awaiting.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public override async Task<T> SendQueryAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Calls.Add(new ApiCall(query, variables, operationName));
            if (responses.Count == 0)
            {
                throw new AssertionException($"Unexpected operation: {operationName}");
            }

            Response response = responses.Dequeue();
            if (response.Exception != null)
            {
                throw response.Exception;
            }
            if (response.Waits)
            {
                awaiting.TrySetResult();
                return await new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }

            return JsonConvert.DeserializeObject<T>(response.Json!) ??
                throw new JsonSerializationException("The simulated response was null.");
        }

        public override Task<ApiResponse<T>> SendQuerySafeAsync<T>(
            string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();

        public override GraphQlApiSubscription<T> GetSubscription<T>(
            Action<Exception> exceptionHandler,
            GraphQlApiSubscription<T>.SubscriptionUpdate subscriptionUpdateHandler,
            string subscription,
            object? variables = null,
            string? operationName = null) => throw new NotImplementedException();

        public override void SetAuthHeader(string jwt) { }
        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotImplementedException();
        public override void SetRole(string role) { }
        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList) { }
        public override void SwitchBack() { }
        public override void DisposeSubscriptions<T>() { }
        protected override void Dispose(bool disposing) { }
    }
}
