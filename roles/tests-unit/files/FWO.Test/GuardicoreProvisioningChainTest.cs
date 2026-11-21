using System.Reflection;
using System.Text.Json.Nodes;
using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;
using RestSharp;

namespace FWO.Test
{
    /// <summary>
    /// Runs the whole Guardicore provisioning chain with the real code and simulated APIs: modelled connection → variance
    /// analysis → request tasks → "Create implementation tasks" → firewall write (with the real data source) against a
    /// simulated Guardicore → re-import of its labels and rules into the next variance analysis. It guards the hand-over
    /// between the steps, e.g. the connection marker and app of the request task the writer needs.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class GuardicoreProvisioningChainTest
    {
        private const int kManagementId = GuardicoreVarianceTestApiConn.kManagementId;
        private const int kGatewayId = 1;
        private const long kTicketId = 3;
        private const int kOpenStateId = 4;
        private const int kCompletedStateId = 10;
        private const string kAppId = "APP-4711";
        private const string kMarker = "FWOC1";
        private const string kRecognitionOption =
            "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true," +
            "\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":false}";

        private static readonly List<string> kTaskTypes =
        [
            nameof(WfTaskType.access), nameof(WfTaskType.rule_modify), nameof(WfTaskType.rule_delete),
            nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete),
            nameof(WfTaskType.object_create), nameof(WfTaskType.object_modify)
        ];
        private static readonly List<string> kRuleTaskTypes = [nameof(WfTaskType.access), nameof(WfTaskType.rule_modify), nameof(WfTaskType.rule_delete)];
        private static readonly List<string> kRangeBlocks = ["10.0.0.0/28", "10.0.0.16/31"];
        private static readonly List<string> kTcpPorts = ["443", "8443"];
        private static readonly List<string> kChangedTcpPorts = ["8080", "8443"];
        private static readonly List<string> kUdpPorts = ["53"];
        private static readonly List<string> kAppServer3 = ["2.2.2.2/32"];

        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1", ExtAppId = kAppId };
        private static readonly ExtStateTestApiConn ExtStateApiConnection = new();
        private static readonly ModellingAppRole AppRole = new()
        {
            Id = 2,
            Name = "AR504711-002",
            IdString = "AR504711-002",
            AppId = Application.Id,
            AppServers = [new() { Content = GuardicoreVarianceTestApiConn.AppServer3 }]
        };

        private static ModellingService Service(int id, string name, int protoId, int port) => new() { Id = id, Name = name, ProtoId = protoId, Port = port, PortEnd = port };

        /// <summary>AppRole → app server range with TCP 443, UDP 53 and a service group with TCP 8443.</summary>
        private static ModellingConnection Connection() => new()
        {
            Id = 1,
            AppId = Application.Id,
            Name = "Conn1",
            SourceAppRoles = [new() { Content = AppRole }],
            DestinationAppServers = [new() { Content = GuardicoreVarianceTestApiConn.AppServerRange }],
            Services = [new() { Content = Service(1, "https", 6, 443) }, new() { Content = Service(2, "dns", 17, 53) }],
            ServiceGroups = [new() { Content = new ModellingServiceGroup { Id = 7, Name = "Admin", AppId = Application.Id, Services = [new() { Content = Service(3, "admin", 6, 8443) }] } }]
        };

        private static ConnectionLabel ConnLabel(int andGroup, string key, string value) =>
            new() { AndGroup = andGroup, Label = new() { Value = value, Key = new() { Name = key } } };

        /// <summary>Guardicore, the FWO side of the workflow and firewall write, and the ids handed out so far.</summary>
        private sealed class Chain
        {
            private long nextReqTaskId = 1;
            private long nextImplTaskId = 1;

            public SimulatedGuardicore Guardicore { get; } = new();
            public InMemoryDeviceSpecificSettingsApiConnection Api { get; } = new();
            public Management Management { get; }
            public Device Gateway { get; }
            public bool LabelBasedModelling { get; init; }

            public Chain()
            {
                Management = new()
                {
                    Id = kManagementId,
                    Name = "Guardicore",
                    Hostname = "gc.example.test",
                    Port = 443,
                    DeviceType = new DeviceType { Id = FirewallVendors.kGuardicoreManagement, Name = "Guardicore Management" },
                    ExportCredential = new Credential { Name = "export", ImportUser = "writer", Secret = "secret" }
                };
                Gateway = new Device { Id = kGatewayId, Name = "Guardicore policy", Management = Management };
                Management.Devices = [Gateway];
                Api.Managements.Add(Management);
                Api.AddNode(1, DeviceSpecificScopeType.Global, "global");
                Api.AddNode(2, DeviceSpecificScopeType.DeviceType, FirewallVendors.kGuardicoreManagement.ToString(), 1);
                Api.AddNode(3, DeviceSpecificScopeType.Management, kManagementId.ToString(), 2);
                Api.AddNode(4, DeviceSpecificScopeType.Gateway, kGatewayId.ToString(), 3);
                Api.SetValue(1, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            }

            /// <summary>
            /// Requests the connections as modelling does (with the current state of Guardicore imported, unless told
            /// otherwise), creates the implementation tasks and writes them in the order of the request tasks.
            /// </summary>
            public async Task<ChainRun> Run(List<ModellingConnection> connections, bool reimport = true)
            {
                List<WfReqTask> requested = await Request(connections, reimport);
                WfTicket ticket = new() { Id = kTicketId, Tasks = requested };
                foreach (WfReqTask reqTask in requested)
                {
                    reqTask.Id = nextReqTaskId++;
                    reqTask.TicketId = kTicketId;
                    reqTask.StateId = kOpenStateId;
                    Api.RequestTaskAdditionalInfo[reqTask.Id] = reqTask.AdditionalInfo ?? "{}";
                }
                await Handler().CreateMissingImplTasksForTicket(ticket);
                List<(WfImplTask Task, ModifyFirewallResult Result)> written = [];
                FirewallWriter writer = new(new ApiFirewallWriteDataSource(Api), new SimulatedFirewallWriteClientFactory(Guardicore.Simulation));
                foreach (WfImplTask implTask in requested.SelectMany(reqTask => reqTask.ImplementationTasks))
                {
                    implTask.Id = nextImplTaskId++;
                    written.Add((implTask, await writer.ModifyFirewall(implTask)));
                }
                return new ChainRun(requested, written);
            }

            private async Task<List<WfReqTask>> Request(List<ModellingConnection> connections, bool reimport)
            {
                GuardicoreVarianceTestApiConn analysisApi = new();
                if (reimport)
                {
                    analysisApi.Rules = Guardicore.ImportedRules(kManagementId, Gateway);
                    analysisApi.ImportedLabels = Guardicore.ImportedLabels();
                }
                else
                {
                    analysisApi.ImportedLabels = [];
                }
                SimulatedUserConfig userConfig = new()
                {
                    ModNamingConvention = "{\"networkAreaRequired\":false,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\",\"applicationZone\":\"AZ\"}",
                    CreateAppZones = false,
                    RuleRecognitionOption = kRecognitionOption,
                    ModRolloutNatHeuristic = false,
                    LabelBasedModelling = LabelBasedModelling
                };
                ModellingVarianceAnalysis analysis = new(analysisApi, new ExtStateHandler(ExtStateApiConnection), userConfig, Application, DefaultInit.DoNothing);
                return await analysis.AnalyseModelledConnectionsForRequest(connections);
            }

            private WfHandler Handler()
            {
                WfHandler handler = new(new SimulatedUserConfig(), Api, WorkflowPhases.planning, null)
                {
                    Devices = [Gateway],
                    Managements = [Management]
                };
                FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
                StateMatrixDict matrices = (StateMatrixDict)field.GetValue(handler)!;
                kTaskTypes.ForEach(taskType => matrices.Matrices[taskType] = new StateMatrix { MinTicketCompleted = kCompletedStateId });
                return handler;
            }
        }

        private sealed record ChainRun(List<WfReqTask> Requested, List<(WfImplTask Task, ModifyFirewallResult Result)> Written)
        {
            public List<WfReqTask> RuleTasks => [.. Requested.Where(task => kRuleTaskTypes.Contains(task.TaskType))];

            public string Failures => string.Join(" | ", Written.Where(write => !write.Result.Success)
                .Select(write => $"{write.Task.TaskType} {write.Task.Title}: {write.Result.Message} {string.Join("; ", write.Result.Steps.Select(step => step.Message))}"));
        }

        private static JsonObject RuleOf(SimulatedGuardicore guardicore, string protocol) =>
            guardicore.Rules.Values.Single(rule => rule["ip_protocols"]![0]!.GetValue<string>() == protocol);

        private static List<string> Texts(JsonNode? array) => [.. (array?.AsArray() ?? []).Select(node => node!.ToJsonString().Trim('"'))];

        private static string LabelId(SimulatedGuardicore guardicore, string key, string value) =>
            guardicore.Labels.Single(label => label.Value["key"]!.GetValue<string>() == key && label.Value["value"]!.GetValue<string>() == value).Key;

        [Test]
        public async Task NewConnection_IsWrittenAndInSyncAfterReimport()
        {
            Chain chain = new();

            ChainRun first = await chain.Run([Connection()]);
            int writesAfterFirstRun = chain.Guardicore.Writes().Count;
            ChainRun second = await chain.Run([Connection()]);

            JsonObject tcp = RuleOf(chain.Guardicore, "TCP");
            JsonObject udp = RuleOf(chain.Guardicore, "UDP");
            string appRoleLabel = LabelId(chain.Guardicore, "AppRole", "AR504711-002");
            WfReqTask access = first.RuleTasks.Single();
            Assert.Multiple(() =>
            {
                Assert.That(first.Failures, Is.Empty);
                Assert.That(access.TaskType, Is.EqualTo(nameof(WfTaskType.access)));
                Assert.That(access.ImplementationTasks.Single().DeviceId, Is.EqualTo(kGatewayId), "one implementation task on the Guardicore gateway");
                Assert.That(chain.Guardicore.Labels[appRoleLabel]["dynamic_criteria"]!.AsArray().Select(criterion => criterion!["argument"]!.GetValue<string>()),
                    Is.EqualTo(kAppServer3), "the app role label holds the address of its app server");
                Assert.That(chain.Guardicore.Rules, Has.Count.EqualTo(2), "one rule per protocol");
                Assert.That(Texts(tcp["ports"]), Is.EqualTo(kTcpPorts), "the service group is resolved into its services");
                Assert.That(Texts(udp["ports"]), Is.EqualTo(kUdpPorts));
                Assert.That(tcp["ruleset_name"]!.GetValue<string>(), Is.EqualTo(kAppId));
                Assert.That(tcp["comments"]!.GetValue<string>(), Is.EqualTo(kMarker));
                Assert.That(Texts(tcp["source"]!["labels"]!["or_labels"]![0]!["and_labels"]), Is.EqualTo((List<string>)[appRoleLabel]));
                Assert.That(Texts(tcp["destination"]!["subnets"]), Is.EqualTo(kRangeBlocks));
                Assert.That(chain.Guardicore.Revisions, Has.Count.EqualTo(1));
                Assert.That(second.Requested, Is.Empty, "after the re-import labels and rules are in sync");
                Assert.That(chain.Guardicore.Writes(), Has.Count.EqualTo(writesAfterFirstRun));
            });
        }

        [Test]
        public async Task RepeatedRequest_WritesNothing()
        {
            Chain chain = new();
            await chain.Run([Connection()]);
            int writes = chain.Guardicore.Writes().Count;

            ChainRun repeated = await chain.Run([Connection()], reimport: false);

            Assert.Multiple(() =>
            {
                Assert.That(repeated.Failures, Is.Empty);
                Assert.That(repeated.RuleTasks.Single().TaskType, Is.EqualTo(nameof(WfTaskType.access)), "without import the connection is requested again");
                Assert.That(chain.Guardicore.Writes(), Has.Count.EqualTo(writes), "the writer finds everything as requested");
                Assert.That(chain.Guardicore.Rules, Has.Count.EqualTo(2));
                Assert.That(chain.Guardicore.Revisions, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ChangedConnection_ReplacesAndDeletesRules()
        {
            Chain chain = new();
            await chain.Run([Connection()]);
            string tcpRuleId = RuleOf(chain.Guardicore, "TCP")["id"]!.GetValue<string>();
            ModellingConnection changed = Connection();
            changed.Services = [new() { Content = Service(1, "http-alt", 6, 8080) }];

            ChainRun change = await chain.Run([changed]);
            ChainRun after = await chain.Run([changed]);

            JsonObject tcp = chain.Guardicore.Rules.Values.Single();
            Assert.Multiple(() =>
            {
                Assert.That(change.Failures, Is.Empty);
                Assert.That(change.RuleTasks.Single().TaskType, Is.EqualTo(nameof(WfTaskType.rule_modify)), "one task for all rules of the connection");
                Assert.That(tcp["id"]!.GetValue<string>(), Is.EqualTo(tcpRuleId), "the TCP rule is replaced, not created again");
                Assert.That(Texts(tcp["ports"]), Is.EqualTo(kChangedTcpPorts));
                Assert.That(chain.Guardicore.Simulation.Requests.Count(request => request.Method == Method.Delete), Is.EqualTo(1), "the UDP rule is deleted");
                Assert.That(chain.Guardicore.Revisions, Has.Count.EqualTo(2));
                Assert.That(after.Requested, Is.Empty);
            });
        }

        [Test]
        public async Task DeletedConnection_DeletesAllItsRules()
        {
            Chain chain = new();
            await chain.Run([Connection()]);
            ModellingConnection documentationOnly = Connection();
            documentationOnly.ExtraConfigs = [new() { ExtraConfigType = "Doku_only" }];

            ChainRun delete = await chain.Run([documentationOnly]);

            Assert.Multiple(() =>
            {
                Assert.That(delete.Failures, Is.Empty);
                Assert.That(delete.RuleTasks.Single().TaskType, Is.EqualTo(nameof(WfTaskType.rule_delete)), "one task for all rules of the connection");
                Assert.That(chain.Guardicore.Rules, Is.Empty);
                Assert.That(chain.Guardicore.Revisions, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task LabelGroupSide_IsWrittenAsAndGroupAndInSyncAfterReimport()
        {
            Chain chain = new() { LabelBasedModelling = true };
            string prod = chain.Guardicore.AddLabel("Stage", "Prod", "2.2.2.0/24");
            string linux = chain.Guardicore.AddLabel("OS", "Linux", "2.2.0.0/16");
            ModellingConnection connection = Connection();
            connection.SourceAppRoles = [];
            connection.SourceLabels = [ConnLabel(0, "Stage", "Prod"), ConnLabel(0, "OS", "Linux")];

            ChainRun first = await chain.Run([connection]);
            ChainRun second = await chain.Run([connection]);

            Assert.Multiple(() =>
            {
                Assert.That(first.Failures, Is.Empty);
                Assert.That(Texts(RuleOf(chain.Guardicore, "TCP")["source"]!["labels"]!["or_labels"]![0]!["and_labels"]), Is.EquivalentTo((List<string>)[prod, linux]));
                Assert.That(second.Requested, Is.Empty);
            });
        }

        [Test]
        public async Task MissingLabel_FailsTheRuleAndNamesIt()
        {
            Chain chain = new() { LabelBasedModelling = true };
            ModellingConnection connection = Connection();
            connection.SourceLabels = [ConnLabel(0, "Stage", "Missing")];

            ChainRun run = await chain.Run([connection]);

            Assert.Multiple(() =>
            {
                Assert.That(run.Failures, Does.Contain("Stage: Missing"));
                Assert.That(chain.Guardicore.Rules, Is.Empty);
                Assert.That(chain.Guardicore.Revisions, Is.Empty);
            });
        }
    }
}
