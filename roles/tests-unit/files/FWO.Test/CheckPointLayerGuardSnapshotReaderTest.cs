using System.Security.Claims;
using FWO.Api.Client;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.CheckPoint;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointLayerGuardSnapshotReaderTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const long kImportId = 100;

    private const string kValidGuardJson = """
        [{
          "rule_id": 10, "mgm_id": 10, "rulebase_id": 1,
          "rule_create": 90, "removed": null,
          "rule_src_neg": false, "rule_dst_neg": false,
          "rule_froms": [{
            "rule_id": 10, "obj_id": 101, "negated": false,
            "rf_create": 90, "removed": null,
            "object": {
              "obj_id": 101, "obj_uid": "src", "obj_name": "source",
              "obj_ip": "10.0.0.0/24", "obj_ip_end": null,
              "obj_create": 90, "removed": null, "type": { "name": "network" },
              "objgrp_flats": []
            }
          }],
          "rule_tos": [{
            "rule_id": 10, "obj_id": 102, "negated": false,
            "rt_create": 90, "removed": null,
            "object": {
              "obj_id": 102, "obj_uid": "dst", "obj_name": "destination",
              "obj_ip": "20.0.0.0/24", "obj_ip_end": null,
              "obj_create": 90, "removed": null, "type": { "name": "network" },
              "objgrp_flats": []
            }
          }]
        }]
        """;

    [Test]
    public async Task ReadAsync_UsesExactImportAndMapsOneGuardPerInlineTarget()
    {
        RecordingApi api = new(kValidGuardJson);
        CheckPointLayerGuardSnapshotReader reader = new(api);

        CheckPointLayerGuardReadSuccess success = (CheckPointLayerGuardReadSuccess)
            await reader.ReadAsync(Graph(), kImportId, CancellationToken.None);

        CheckPointLayerGuard guard = success.Snapshot.GuardsByTargetLayerId[2];
        JObject variables = JObject.FromObject(api.Variables!);
        Assert.Multiple(() =>
        {
            Assert.That((int?)variables["managementId"], Is.EqualTo(kManagementId));
            Assert.That((long?)variables["importId"], Is.EqualTo(kImportId));
            Assert.That(variables["sourceRuleIds"]!.Values<long>(), Is.EqualTo(new long[] { 10 }));
            Assert.That(api.OperationName, Is.EqualTo("getCheckPointLayerGuardsAtImport"));
            Assert.That(success.Snapshot.ImportId, Is.EqualTo(kImportId));
            Assert.That(guard.SourceRuleId, Is.EqualTo(10));
            Assert.That(guard.Source.Terms.Single().IncludedRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.0"));
            Assert.That(guard.Destination.Terms.Single().IncludedRanges.Single().End.ToString(), Is.EqualTo("20.0.0.255"));
        });
    }

    [Test]
    public async Task FlattenedGroup_PreservesKnownExclusionsAndUncertainty()
    {
        JArray json = JArray.Parse(kValidGuardJson);
        JObject sourceObject = (JObject)json[0]!["rule_froms"]![0]!["object"]!;
        sourceObject["obj_ip"] = null;
        sourceObject["obj_ip_end"] = null;
        sourceObject["objgrp_flats"] = JArray.Parse("""
            [
              { "objgrp_flat_id": 101, "objgrp_flat_member_id": 201, "negated": false,
                "import_created": 90, "removed": null,
                "object": { "obj_id": 201, "obj_ip": "10.0.0.0/24", "obj_ip_end": null,
                            "obj_create": 90, "removed": null, "objgrp_flats": [] } },
              { "objgrp_flat_id": 101, "objgrp_flat_member_id": 202, "negated": true,
                "import_created": 90, "removed": null,
                "object": { "obj_id": 202, "obj_ip": "10.0.0.10", "obj_ip_end": null,
                            "obj_create": 90, "removed": null, "objgrp_flats": [] } },
              { "objgrp_flat_id": 101, "objgrp_flat_member_id": 203, "negated": false,
                "import_created": 90, "removed": null,
                "object": { "obj_id": 203, "obj_ip": null, "obj_ip_end": null,
                            "obj_create": 90, "removed": null, "objgrp_flats": [] } }
            ]
            """);

        CheckPointLayerGuardReadSuccess success = (CheckPointLayerGuardReadSuccess)
            await new CheckPointLayerGuardSnapshotReader(new RecordingApi(json.ToString(Formatting.None)))
                .ReadAsync(Graph(), kImportId, CancellationToken.None);

        CheckPointGuardAddressTerm term = success.Snapshot.GuardsByTargetLayerId[2].Source.Terms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(term.IncludedRanges, Has.Count.EqualTo(1));
            Assert.That(term.ExcludedRanges, Has.Count.EqualTo(1));
            Assert.That(term.HasUnresolvedIncludedTerms, Is.True);
            Assert.That(term.HasUnresolvedExcludedTerms, Is.False);
        });
    }

    [TestCase("rule_src_neg")]
    [TestCase("rulebase_id")]
    [TestCase("rule_create")]
    public async Task InvalidGuardData_FailsClosed(string property)
    {
        JArray json = JArray.Parse(kValidGuardJson);
        json[0]![property] = property switch
        {
            "rule_src_neg" => true,
            "rulebase_id" => 99,
            _ => 101
        };

        CheckPointLayerGuardReadFailure failure = (CheckPointLayerGuardReadFailure)
            await new CheckPointLayerGuardSnapshotReader(new RecordingApi(json.ToString(Formatting.None)))
                .ReadAsync(Graph(), kImportId, CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(CheckPointLayerGuardReadFailureCategory.InvalidGuardData));
    }

    [Test]
    public async Task NoInlineLinks_ReturnsEmptySnapshotWithoutQuery()
    {
        RecordingApi api = new(kValidGuardJson);
        Rulebase main = Rulebase(1, "main", Rule(10, "rule", 1));
        RulebaseGraphSnapshot graph = new(kManagementId, kGatewayId, [main],
            [Link(null, null, 1, RulebaseLinkTypes.Ordered, true)]);

        CheckPointLayerGuardReadSuccess success = (CheckPointLayerGuardReadSuccess)
            await new CheckPointLayerGuardSnapshotReader(api).ReadAsync(graph, kImportId, CancellationToken.None);

        Assert.That(success.Snapshot.GuardsByTargetLayerId, Is.Empty);
        Assert.That(api.CallCount, Is.Zero);
    }

    [Test]
    public async Task QueryFailure_IsSanitizedAndCancellationPropagates()
    {
        RecordingApi failedApi = new(new InvalidOperationException("secret"));
        CheckPointLayerGuardReadFailure failure = (CheckPointLayerGuardReadFailure)
            await new CheckPointLayerGuardSnapshotReader(failedApi).ReadAsync(Graph(), kImportId, CancellationToken.None);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(CheckPointLayerGuardReadFailureCategory.QueryFailed));
            Assert.That(failure.Diagnostic, Does.Not.Contain("secret"));
            Assert.ThrowsAsync<OperationCanceledException>(() =>
                new CheckPointLayerGuardSnapshotReader(new RecordingApi(kValidGuardJson))
                    .ReadAsync(Graph(), kImportId, cancellation.Token));
        });
    }

    private static RulebaseGraphSnapshot Graph()
    {
        Rulebase main = Rulebase(1, "main", Rule(10, "guard", 1));
        Rulebase inline = Rulebase(2, "inline");
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId, [main, inline],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline)
        ]);
    }

    private static Rulebase Rulebase(int id, string uid, params Rule[] rules)
    {
        foreach (Rule rule in rules)
        {
            rule.MgmtId = kManagementId;
            rule.RulebaseId = id;
        }
        return new Rulebase { Id = id, Uid = uid, Name = uid, MgmtId = kManagementId, Rules = rules };
    }

    private static Rule Rule(int id, string uid, double order) =>
        new() { Id = id, Uid = uid, RuleNumNumeric = order };

    private static RulebaseLink Link(int? fromRulebase, int? fromRule, int target, int type, bool initial = false) => new()
    {
        GatewayId = kGatewayId,
        FromRulebaseId = fromRulebase,
        FromRuleId = fromRule,
        NextRulebaseId = target,
        LinkType = type,
        IsInitial = initial
    };

    private sealed class RecordingApi : ApiConnection
    {
        private readonly string? json;
        private readonly Exception? exception;
        internal int CallCount { get; private set; }
        internal object? Variables { get; private set; }
        internal string? OperationName { get; private set; }

        internal RecordingApi(string json) => this.json = json;
        internal RecordingApi(Exception exception) => this.exception = exception;

        public override Task<T> SendQueryAsync<T>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            CallCount++;
            Variables = variables;
            OperationName = operationName;
            if (exception != null)
            {
                throw exception;
            }
            return Task.FromResult(JsonConvert.DeserializeObject<T>(json!)!);
        }

        public override Task<ApiResponse<T>> SendQuerySafeAsync<T>(string query, object? variables = null,
            string? operationName = null) => throw new NotImplementedException();
        public override GraphQlApiSubscription<T> GetSubscription<T>(Action<Exception> exceptionHandler,
            GraphQlApiSubscription<T>.SubscriptionUpdate subscriptionUpdateHandler, string subscription,
            object? variables = null, string? operationName = null) => throw new NotImplementedException();
        public override void SetAuthHeader(string jwt) { }
        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotImplementedException();
        public override void SetRole(string role) { }
        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList) { }
        public override void SwitchBack() { }
        public override void DisposeSubscriptions<T>() { }
        protected override void Dispose(bool disposing) { }
    }
}
