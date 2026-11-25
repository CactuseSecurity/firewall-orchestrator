using System.Text.Json.Nodes;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;

namespace FWO.Test
{
    /// <summary>
    /// Guards the rule writes to the Guardicore Allow section against a simulated REST API: one rule per protocol, found
    /// again by the connection marker in the comment, label names replaced by ids, other rules of the connection deleted,
    /// and a policy revision published only after a change.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class GuardicoreWriteClientRulesTest
    {
        private const string kAuthenticatePath = "/api/v3.0/authenticate";
        private const string kLabelsPath = "/api/v4.0/labels";
        private const string kRulesPath = "/api/v4.0/visibility/policy/rules";
        private const string kRevisionsPath = "/api/v4.0/visibility/policy/revisions";
        private const string kRuleDeletePath = kRulesPath + "/delete";
        private const string kRuleUndeletePath = kRulesPath + "/undelete";
        private const string kMarker = "FWOC12";
        private const string kAppRole = "AR504711-001";

        private static readonly List<string> kLabelId = ["lab-1"];
        private static readonly List<string> kHost = ["10.0.0.5/32"];
        private static readonly List<string> kTcpAndUdp = ["TCP", "UDP"];
        private static readonly List<string> kIcmpRuleDeleted = [$"{kRuleDeletePath}/r-icmp"];
        private static readonly List<string> kBothRulesDeleted = [$"{kRuleDeletePath}/r-tcp", $"{kRuleDeletePath}/r-udp"];
        private static readonly List<string> kBothRulesUndeletedInReverse = [$"{kRuleUndeletePath}/r-udp", $"{kRuleUndeletePath}/r-tcp"];

        private static FirewallWriteContext Context() =>
            FirewallWriteTestSupport.Context(FirewallVendors.kGuardicoreManagement) with { ConnectionMarker = kMarker, ExtAppId = "APP-1" };

        private static WfImplTask AccessTask(WfTaskType taskType = WfTaskType.access)
        {
            WfImplElement source = new() { GroupName = $"AppRole: {kAppRole}", Field = nameof(ElemFieldType.source), ImplAction = nameof(RequestAction.create) };
            return FirewallWriteTestSupport.Task(taskType, source, FirewallWriteTestSupport.Network("host", "10.0.0.5", null, ElemFieldType.destination),
                FirewallWriteTestSupport.Service("https", 6, 443), FirewallWriteTestSupport.Service("dns", 17, 53));
        }

        /// <summary>The rule FWO writes for the TCP part of the access task, as the rule list would return it.</summary>
        private static JsonObject WrittenTcpRule(string id, string comment = kMarker) => new()
        {
            ["id"] = id,
            ["section_position"] = "ALLOW",
            ["action"] = "ALLOW",
            ["enabled"] = true,
            ["ruleset_name"] = "APP-1",
            ["comments"] = comment,
            ["ip_protocols"] = new JsonArray("TCP"),
            ["ports"] = new JsonArray(443),
            ["port_ranges"] = new JsonArray(),
            ["source"] = new JsonObject { ["labels"] = new JsonObject { ["or_labels"] = new JsonArray(new JsonObject { ["and_labels"] = new JsonArray(new JsonObject { ["id"] = "lab-1", ["key"] = "AppRole" }) }) } },
            ["destination"] = new JsonObject { ["subnets"] = new JsonArray("10.0.0.5/32") }
        };

        private static JsonObject OtherRule(string id, string protocol, string comment = kMarker, string section = "ALLOW") => new()
        {
            ["id"] = id,
            ["section_position"] = section,
            ["comments"] = comment,
            ["ip_protocols"] = new JsonArray(protocol)
        };

        /// <summary>A Guardicore that knows the label of the app role (unless told otherwise) and the given rules.</summary>
        private static FirewallSimulation Simulation(List<JsonObject> rules, bool labelExists = true)
        {
            FirewallSimulation simulation = new();
            string labels = labelExists ? $$"""{"objects":[{"id":"lab-1","key":"AppRole","value":"{{kAppRole}}"}]}""" : """{"objects":[]}""";
            JsonObject ruleList = new() { ["objects"] = new JsonArray([.. rules.Select(rule => (JsonNode)rule.DeepClone())]), ["total_count"] = rules.Count };
            simulation.Handler = request => (request.Method, request.Resource) switch
            {
                (_, kAuthenticatePath) => FirewallWriteTestSupport.Ok("""{"access_token":"tok"}"""),
                (Method.Get, kLabelsPath) => FirewallWriteTestSupport.Ok(labels),
                (Method.Get, kRulesPath) => FirewallWriteTestSupport.Ok(ruleList.ToJsonString()),
                (Method.Post, kRulesPath) => FirewallWriteTestSupport.Ok("""{"id":"new-rule"}"""),
                _ => FirewallWriteTestSupport.Ok("{}")
            };
            return simulation;
        }

        private static Task<ModifyFirewallResult> Write(FirewallSimulation simulation, WfImplTask task) =>
            new FirewallWriter(new FixedFirewallWriteDataSource(Context()), new SimulatedFirewallWriteClientFactory(simulation)).ModifyFirewall(task);

        private static List<RecordedFirewallRequest> Calls(FirewallSimulation simulation, Method method, string resource) =>
            [.. simulation.Requests.Where(request => request.Method == method && request.Resource == resource)];

        /// <summary>The paths of the POST calls below a rules path, e.g. the rule deletions.</summary>
        private static List<string> RuleActions(FirewallSimulation simulation, string rulesPath) =>
            [.. simulation.Requests.Where(request => request.Method == Method.Post && request.Resource.StartsWith(rulesPath + "/", StringComparison.Ordinal))
                .Select(request => request.Resource)];

        [Test]
        public async Task NewConnection_CreatesOneRulePerProtocolAndPublishes()
        {
            FirewallSimulation simulation = Simulation([]);

            ModifyFirewallResult result = await Write(simulation, AccessTask());

            List<JsonNode> created = [.. Calls(simulation, Method.Post, kRulesPath).Select(request => JsonNode.Parse(request.Body!)!)];
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(created.Select(rule => rule["ip_protocols"]![0]!.GetValue<string>()), Is.EquivalentTo(kTcpAndUdp));
                Assert.That(created.Single(rule => rule["ip_protocols"]![0]!.GetValue<string>() == "UDP")["ports"]!.ToJsonString(), Is.EqualTo("[53]"));
                Assert.That(created[0]["source"]!["labels"]!["or_labels"]![0]!["and_labels"]!.AsArray().Select(id => id!.GetValue<string>()), Is.EqualTo(kLabelId),
                    "label names are replaced by their ids");
                Assert.That(created[0]["destination"]!["subnets"]!.AsArray().Select(subnet => subnet!.GetValue<string>()), Is.EqualTo(kHost));
                Assert.That(created[0]["comments"]!.GetValue<string>(), Is.EqualTo(kMarker));
                Assert.That(created[0]["ruleset_name"]!.GetValue<string>(), Is.EqualTo("APP-1"));
                Assert.That(created[0]["section_position"]!.GetValue<string>(), Is.EqualTo("ALLOW"));
                Assert.That(Calls(simulation, Method.Get, kLabelsPath), Has.Count.EqualTo(1), "each label is looked up once");
                Assert.That(Calls(simulation, Method.Get, kRulesPath), Has.Count.EqualTo(1), "the rules are read once per task");
                Assert.That(JsonNode.Parse(Calls(simulation, Method.Post, kRevisionsPath).Single().Body!)!["comments"]!.GetValue<string>(), Does.Contain("FWO ticket 3"));
            });
        }

        [Test]
        public async Task UnchangedRules_AreNotWrittenAgainAndNothingIsPublished()
        {
            JsonObject udp = WrittenTcpRule("r-udp");
            udp["ip_protocols"] = new JsonArray("UDP");
            udp["ports"] = new JsonArray(53);
            FirewallSimulation simulation = Simulation([WrittenTcpRule("r-tcp"), udp]);

            ModifyFirewallResult result = await Write(simulation, AccessTask());

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests.Where(request => request.Method is Method.Post or Method.Put or Method.Delete && request.Resource != kAuthenticatePath),
                    Is.Empty, "a repeated request changes nothing");
            });
        }

        [Test]
        public async Task ChangedRule_IsReplacedAndOtherRulesOfTheConnectionAreDeleted()
        {
            JsonObject changed = WrittenTcpRule("r-tcp");
            changed["ports"] = new JsonArray(8443);
            FirewallSimulation simulation = Simulation(
            [
                changed,
                OtherRule("r-icmp", "ICMP"),
                OtherRule("r-other-connection", "ICMP", "FWOC123"),
                OtherRule("r-other-section", "ICMP", kMarker, "BLOCK")
            ]);

            ModifyFirewallResult result = await Write(simulation, AccessTask(WfTaskType.rule_modify));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(JsonNode.Parse(Calls(simulation, Method.Put, $"{kRulesPath}/r-tcp").Single().Body!)!["ports"]!.ToJsonString(), Is.EqualTo("[443]"));
                Assert.That(Calls(simulation, Method.Post, kRulesPath), Has.Count.EqualTo(1), "the UDP rule is created");
                Assert.That(RuleActions(simulation, kRuleDeletePath), Is.EqualTo(kIcmpRuleDeleted), "only the rule of the connection in the Allow section");
                Assert.That(simulation.Requests.Exists(request => request.Method == Method.Delete), Is.False, "the API offers no DELETE for rules");
                Assert.That(Calls(simulation, Method.Post, kRevisionsPath), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RuleDelete_DeletesAllRulesOfTheConnection()
        {
            FirewallSimulation simulation = Simulation([WrittenTcpRule("r-tcp"), OtherRule("r-udp", "UDP"), OtherRule("r-other", "UDP", "FWOC1")]);

            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.rule_delete, FirewallWriteTestSupport.Rule("r-tcp")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(RuleActions(simulation, kRuleDeletePath), Is.EquivalentTo(kBothRulesDeleted));
                Assert.That(Calls(simulation, Method.Post, kRevisionsPath), Has.Count.EqualTo(1));
            });
        }

        /// <summary>Rules deleted before a failure are undeleted in reverse order, keeping their ids, instead of being created again.</summary>
        [Test]
        public async Task FailedPublish_UndeletesTheDeletedRules()
        {
            FirewallSimulation simulation = Simulation([WrittenTcpRule("r-tcp"), OtherRule("r-udp", "UDP")]);
            Func<RecordedFirewallRequest, RestResponse> answer = simulation.Handler;
            simulation.Handler = request => request.Method == Method.Post && request.Resource == kRevisionsPath
                ? FirewallWriteTestSupport.Response(System.Net.HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}")
                : answer(request);

            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.rule_delete, FirewallWriteTestSupport.Rule("r-tcp")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(RuleActions(simulation, kRuleDeletePath), Is.EquivalentTo(kBothRulesDeleted));
                Assert.That(RuleActions(simulation, kRuleUndeletePath), Is.EqualTo(kBothRulesUndeletedInReverse));
                Assert.That(Calls(simulation, Method.Post, kRulesPath), Is.Empty, "no rule is created again");
            });
        }

        [Test]
        public async Task RuleList_IsPagedWithTheDocumentedAndTheOlderParameters()
        {
            FirewallSimulation simulation = Simulation([WrittenTcpRule("r-tcp")]);

            await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.rule_delete, FirewallWriteTestSupport.Rule("r-tcp")));

            RecordedFirewallRequest list = Calls(simulation, Method.Get, kRulesPath).First();
            Assert.Multiple(() =>
            {
                Assert.That(list.QueryParameters["start_at"], Is.EqualTo("0"));
                Assert.That(list.QueryParameters["offset"], Is.EqualTo("0"));
                Assert.That(list.QueryParameters["max_results"], Is.EqualTo("1000"));
                Assert.That(list.QueryParameters["limit"], Is.EqualTo("1000"));
            });
        }

        [Test]
        public async Task MissingLabel_FailsAndNamesIt()
        {
            FirewallSimulation simulation = Simulation([], labelExists: false);

            ModifyFirewallResult result = await Write(simulation, AccessTask());

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(result.Steps.Last().Message, Does.Contain($"AppRole: {kAppRole}"));
                Assert.That(Calls(simulation, Method.Post, kRulesPath), Is.Empty);
                Assert.That(Calls(simulation, Method.Post, kRevisionsPath), Is.Empty);
            });
        }

        [Test]
        public void Markers_AreWholeTokens()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GuardicoreRules.HasMarker("FWOC12", kMarker), Is.True);
                Assert.That(GuardicoreRules.HasMarker("FWOC12, ComSvc", kMarker), Is.True);
                Assert.That(GuardicoreRules.HasMarker("see (FWOC12)", kMarker), Is.True);
                Assert.That(GuardicoreRules.HasMarker("FWOC123", kMarker), Is.False);
                Assert.That(GuardicoreRules.HasMarker("XFWOC12", kMarker), Is.False);
                Assert.That(GuardicoreRules.HasMarker("FWOC123 FWOC12", kMarker), Is.True);
                Assert.That(GuardicoreRules.HasMarker("FWOC12x", kMarker), Is.False, "a marker ends at a word boundary");
                Assert.That(GuardicoreRules.HasMarker(null, kMarker), Is.False);
            });
        }

        /// <summary>A marker of another form, e.g. a shortened one, would match the markers of all connections: it matches none.</summary>
        [Test]
        public void MalformedMarkers_MatchNothing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GuardicoreRules.HasMarker("FWOC12", "F"), Is.False);
                Assert.That(GuardicoreRules.HasMarker("FWOC12", "FWOC"), Is.False);
                Assert.That(GuardicoreRules.HasMarker("12", "12"), Is.False);
                Assert.That(GuardicoreRules.HasMarker("FWO C12", "FWO C12"), Is.False);
                Assert.That(GuardicoreRules.TryParseMarker("FWOC12", out string connectionId), Is.True);
                Assert.That(connectionId, Is.EqualTo("12"));
                Assert.That(GuardicoreRules.TryParseMarker("FWOC", out _), Is.False);
            });
        }

        /// <summary>
        /// A task that fails after rule changes undoes them before anything is published: created rules are deleted, replaced
        /// ones restored and deleted ones undeleted, so no half-written connection waits for the next revision.
        /// </summary>
        [Test]
        public async Task FailedTask_UndoesItsRuleChanges()
        {
            JsonObject changed = WrittenTcpRule("r-tcp");
            changed["ports"] = new JsonArray(8443);
            FirewallSimulation simulation = Simulation([changed, OtherRule("r-icmp", "ICMP")]);
            Func<RecordedFirewallRequest, RestResponse> answer = simulation.Handler;
            simulation.Handler = request => request.Method == Method.Post && request.Resource == kRulesPath && request.Body!.Contains("\"UDP\"")
                ? FirewallWriteTestSupport.Response(System.Net.HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}")
                : answer(request);

            ModifyFirewallResult result = await Write(simulation, AccessTask(WfTaskType.rule_modify));

            List<RecordedFirewallRequest> afterFailure = [.. simulation.Requests.SkipWhile(request => !(request.Method == Method.Post && request.Body!.Contains("\"UDP\""))).Skip(1)];
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(Calls(simulation, Method.Put, $"{kRulesPath}/r-tcp"), Has.Count.EqualTo(2), "the TCP rule was replaced before the failure, then restored");
                Assert.That(afterFailure.Single(request => request.Method == Method.Put).Resource, Is.EqualTo($"{kRulesPath}/r-tcp"), "and is restored");
                Assert.That(JsonNode.Parse(afterFailure.Single(request => request.Method == Method.Put).Body!)!["ports"]!.ToJsonString(), Is.EqualTo("[8443]"));
                Assert.That(Calls(simulation, Method.Post, kRevisionsPath), Is.Empty, "nothing is published");
            });
        }

        [Test]
        public async Task FailedTask_DeletesTheRulesItCreated()
        {
            FirewallSimulation simulation = Simulation([]);
            Func<RecordedFirewallRequest, RestResponse> answer = simulation.Handler;
            simulation.Handler = request => request.Method == Method.Post && request.Resource == kRulesPath && request.Body!.Contains("\"UDP\"")
                ? FirewallWriteTestSupport.Response(System.Net.HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}")
                : answer(request);

            ModifyFirewallResult result = await Write(simulation, AccessTask());

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(Calls(simulation, Method.Post, $"{kRuleDeletePath}/new-rule"), Has.Count.EqualTo(1), "the created TCP rule is deleted again");
                Assert.That(Calls(simulation, Method.Post, kRevisionsPath), Is.Empty);
            });
        }

        [Test]
        public void AllowRulebases_AreRecognizedByTheImportedName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GuardicoreWriteDefaults.IsAllowRulebase("04 Allow APP-1234"), Is.True);
                Assert.That(GuardicoreWriteDefaults.IsAllowRulebase("04 Allow"), Is.True);
                Assert.That(GuardicoreWriteDefaults.IsAllowRulebase("01 Override Allow APP-1234"), Is.False);
                Assert.That(GuardicoreWriteDefaults.IsAllowRulebase("05 Alert APP-1234"), Is.False);
                Assert.That(GuardicoreWriteDefaults.IsAllowRulebase("04 Allowed"), Is.False);
            });
        }

        [Test]
        public void Matches_IgnoresOrderAndLabelRepresentationButNotOtherSideFields()
        {
            JsonObject target = WrittenTcpRule("x");
            target["source"] = new JsonObject { ["labels"] = new JsonObject { ["or_labels"] = new JsonArray(new JsonObject { ["and_labels"] = new JsonArray("lab-1") }) } };
            JsonObject withAssets = WrittenTcpRule("x");
            withAssets["destination"]!["assets"] = new JsonArray("asset-1");
            JsonObject disabled = WrittenTcpRule("x");
            disabled["enabled"] = false;
            Assert.Multiple(() =>
            {
                Assert.That(GuardicoreRules.Matches(WrittenTcpRule("r"), target), Is.True);
                Assert.That(GuardicoreRules.Matches(withAssets, target), Is.False);
                Assert.That(GuardicoreRules.Matches(disabled, target), Is.False);
            });
        }
    }
}
