using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using GraphQL;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Runs the same connections through the three variance views - report, connection status and
    /// request - and checks that they come to the same verdict (#5715).
    /// </summary>
    [TestFixture]
    internal class ModellingVarianceConsistencyTest
    {
        private const int kMgtId = 1;
        private const long kDummyAppRoleId = 99;
        private const string kGroupNameRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":true,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}";
        private const string kResolveGroupRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":true,\"nwSeparateGroupAnalysis\":false,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}";
        private const string kNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}";
        private const string kDifferingRuleUid = "uid-second-rule";

        private static readonly FwoOwner Owner = new() { Id = 1, Name = "App1" };
        private static readonly ExtStateHandler ExtStates = new(new ExtStateTestApiConn());

        /// <summary>
        /// Production state: groups for app roles 001, 002, 003 and 009, two rules for connection 9
        /// (one fitting, one with a different app role set, like a second rule in another section),
        /// one rule for connection 11 using group 003, which has a host the model does not have.
        /// </summary>
        private sealed class ConsistencyTestApiConn : SimulatedApiConnection
        {
            private static NetworkObject Host(long id, string name, string ip) => new() { Id = id, Name = name, IP = ip, IpEnd = ip, Type = new() { Name = ObjectType.Host } };
            private static NetworkObject HostA() => Host(10, "HostA", "10.0.0.1/32");
            private static NetworkObject HostB() => Host(11, "HostB", "10.0.0.2/32");

            private static NetworkObject Group(long id, string name, params NetworkObject[] members) => new()
            {
                Id = id,
                Name = name,
                Type = new() { Name = ObjectType.Group },
                ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject>() { Object = member })]
            };

            private static NetworkObject Group1() => Group(1, "AR504711-001", HostA(), HostB());
            private static NetworkObject Group2() => Group(2, "AR504711-002", HostA());
            private static NetworkObject Group3() => Group(3, "AR504711-003", HostA(), HostB());
            private static NetworkObject Group9() => Group(9, "AR504711-009", HostA());

            private static NetworkService Dns() => new() { Id = 1, Name = "dns", DestinationPort = 53, DestinationPortEnd = 53, ProtoId = 17 };

            private static Rule CreateRule(string uid, string name, List<NetworkObject> froms, List<NetworkObject> tos) => new()
            {
                Uid = uid,
                Name = name,
                MgmtId = kMgtId,
                Froms = [.. froms.Select(obj => new NetworkLocation(new(), obj))],
                Tos = [.. tos.Select(obj => new NetworkLocation(new(), obj))],
                Services = [new() { Content = Dns() }],
                EnforcingGateways = [new() { Content = new() { Id = 1, Name = "Gateway1" } }]
            };

            private static List<Rule> CreateRules()
            {
                // fresh objects per query: the analysis marks surplus objects on them
                List<NetworkObject> fittingFroms = [Group1()];
                List<NetworkObject> differingFroms = [Group1(), Group9()];
                List<NetworkObject> resolveFroms = [Group3()];
                List<NetworkObject> fittingTos = [Group2()];
                List<NetworkObject> differingTos = [Group2()];
                List<NetworkObject> resolveTos = [Group2()];
                return
                [
                    CreateRule("uid-fitting-rule", "FWOC9", fittingFroms, fittingTos),
                    CreateRule(kDifferingRuleUid, "FWOC9", differingFroms, differingTos),
                    CreateRule("uid-resolve-rule", "FWOC11", resolveFroms, resolveTos)
                ];
            }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                await DefaultInit.DoNothing();
                object data = typeof(QueryResponseType) switch
                {
                    Type t when t == typeof(List<ManagementReport>) && query == ReportQueries.getRelevantImportIdsAtTime =>
                        new List<ManagementReport> { new() { Id = kMgtId, Import = new() { ImportAggregate = new() { ImportAggregateMax = new() { RelevantImportId = 1 } } } } },
                    Type t when t == typeof(List<Management>) =>
                        new List<Management> { new() { Id = kMgtId, Name = "Mgt1", ExtMgtData = "{\"id\":\"1\",\"name\":\"Ext\"}", Devices = [new() { Id = 1 }] } },
                    Type t when t == typeof(List<NetworkObject>) => ObjectsFor(variables),
                    Type t when t == typeof(List<Rule>) => RulesFor(variables),
                    Type t when t == typeof(List<ModellingConnection>) => new List<ModellingConnection>(),
                    Type t when t == typeof(List<ModellingNetworkArea>) => new List<ModellingNetworkArea>(),
                    Type t when t == typeof(List<DeviceReport>) => new List<DeviceReport>(),
                    _ => throw new NotImplementedException($"{typeof(QueryResponseType)}: {query}")
                };
                return (QueryResponseType)data;
            }

            private static List<NetworkObject> ObjectsFor(object? variables)
            {
                int[] objTypeIds = GetVariable<int[]>(variables, "objTypeIds");
                return objTypeIds[0] == 2 ? [Group1(), Group2(), Group3(), Group9()] : [HostA(), HostB()];
            }

            private static List<Rule> RulesFor(object? variables)
            {
                // the rule query is paged; all rules fit on the first page
                return GetVariable<int>(variables, "offset") == 0 ? CreateRules() : [];
            }

            private static T GetVariable<T>(object? variables, string name)
            {
                if (variables is IDictionary<string, object?> dict)
                {
                    return (T)dict[name]!;
                }
                return (T)variables!.GetType().GetProperty(name)!.GetValue(variables)!;
            }
        }

        private static readonly ModellingAppServer AppServerA = new() { Id = 1, Name = "HostA", Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32" };
        private static readonly ModellingAppServer DeletedAppServerB = new() { Id = 2, Name = "HostB", Ip = "10.0.0.2/32", IpEnd = "10.0.0.2/32", IsDeleted = true };

        /// <summary>
        /// The case from the issue: an app role with a deleted app server, and two production rules
        /// carrying the marker of the connection, one of them differing.
        /// </summary>
        private static ModellingConnection CreateConnectionWithDeletedAppServer()
        {
            ModellingAppRole appRoleWithDeleted = new() { Id = 1, AppId = Owner.Id, Name = "AppRole1", IdString = "AR504711-001", AppServers = [new() { Content = AppServerA }, new() { Content = DeletedAppServerB }] };
            ModellingAppRole appRole2 = new() { Id = 2, AppId = Owner.Id, Name = "AppRole2", IdString = "AR504711-002", AppServers = [new() { Content = AppServerA }] };
            return new()
            {
                Id = 9,
                AppId = Owner.Id,
                Name = "Conn9",
                SourceAppRoles = [new() { Content = appRoleWithDeleted }],
                DestinationAppRoles = [new() { Content = appRole2 }],
                Services = [new() { Content = new() { Id = 1, Name = "dns", Port = 53, ProtoId = 17 } }]
            };
        }

        /// <summary>
        /// Connection whose source app role misses a host the production group has (HostB).
        /// </summary>
        private static ModellingConnection CreateConnectionWithSurplusGroupMember()
        {
            ModellingAppRole appRole3 = new() { Id = 3, AppId = Owner.Id, Name = "AppRole3", IdString = "AR504711-003", AppServers = [new() { Content = AppServerA }] };
            ModellingAppRole appRole2 = new() { Id = 2, AppId = Owner.Id, Name = "AppRole2", IdString = "AR504711-002", AppServers = [new() { Content = AppServerA }] };
            return new()
            {
                Id = 11,
                AppId = Owner.Id,
                Name = "Conn11",
                SourceAppRoles = [new() { Content = appRole3 }],
                DestinationAppRoles = [new() { Content = appRole2 }],
                Services = [new() { Content = new() { Id = 1, Name = "dns", Port = 53, ProtoId = 17 } }]
            };
        }

        private static SimulatedUserConfig CreateUserConfig(bool rolloutRemoved, string recognitionOption) => new()
        {
            ModNamingConvention = kNamingConvention,
            RuleRecognitionOption = recognitionOption,
            ModRolloutRemovedAppServers = rolloutRemoved,
            CreateAppZones = false
        };

        private static ModellingVarianceAnalysis CreateAnalysis(SimulatedUserConfig userConfig)
        {
            return new(new ConsistencyTestApiConn(), ExtStates, userConfig, Owner, DefaultInit.DoNothing);
        }

        private static async Task<OwnerConnectionReport> RunReport(ModellingConnection connection, SimulatedUserConfig userConfig)
        {
            OwnerConnectionReport ownerReport = new(kDummyAppRoleId) { Owner = Owner, Connections = [connection] };
            ReportGenerator.SelectConnectionsToAnalyse(ownerReport, userConfig);
            ModellingVarianceResult result = await CreateAnalysis(userConfig).AnalyseRulesVsModelledConnections(ownerReport.Connections, new());
            ownerReport.Connections = result.ConnsNotImplemented;
            ownerReport.RuleDifferences = result.RuleDifferences;
            return ownerReport;
        }

        private static async Task<ModellingConnection> RunStatus(ModellingConnection connection, SimulatedUserConfig userConfig)
        {
            connection.SyncState(kDummyAppRoleId, userConfig.ModRolloutRemovedAppServers);
            List<ModellingConnection> connections = [connection];
            await CreateAnalysis(userConfig).AnalyseConnsForStatus(connections, kDummyAppRoleId);
            return connection;
        }

        private static async Task<List<WfReqTask>> RunRequest(ModellingConnection connection, SimulatedUserConfig userConfig)
        {
            List<ModellingConnection> connections = [connection];
            List<ModellingConnection> toRequest = ModellingRequestConnectionSelector.ForRegularRequests(connections, userConfig, kDummyAppRoleId);
            return await CreateAnalysis(userConfig).AnalyseModelledConnectionsForRequest(toRequest);
        }

        [Test]
        public async Task DeletedAppServerWithRolloutRemoved_AllViewsReportTheDifferingRule()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(true, kGroupNameRecogOpt);

            OwnerConnectionReport report = await RunReport(CreateConnectionWithDeletedAppServer(), userConfig);
            ModellingConnection status = await RunStatus(CreateConnectionWithDeletedAppServer(), userConfig);
            List<WfReqTask> tasks = await RunRequest(CreateConnectionWithDeletedAppServer(), userConfig);

            List<WfReqTask> ruleTasks = [.. tasks.Where(t => t.TaskType == WfTaskType.rule_modify.ToString())];
            Assert.Multiple(() =>
            {
                Assert.That(report.ExcludedConnections, Is.Empty, "report must analyse the connection");
                Assert.That(report.Connections, Is.Empty, "the connection is implemented");
                Assert.That(report.RuleDifferences, Has.Count.EqualTo(1));
                Assert.That(report.RuleDifferences[0].ImplementedRules.Select(r => r.Uid), Is.EquivalentTo(DifferingRuleUids));

                Assert.That(status.GetBoolProperty(ConState.VarianceChecked.ToString()), Is.True);
                Assert.That(status.GetBoolProperty(ConState.VarianceFound.ToString()), Is.True);
                Assert.That(status.GetBoolProperty(ConState.NotImplemented.ToString()), Is.False);

                Assert.That(ruleTasks, Has.Count.EqualTo(1));
                Assert.That(ruleTasks[0].GetRuleElements().Single().RuleUid, Is.EqualTo(kDifferingRuleUid));
                Assert.That(ruleTasks[0].RequestAction, Is.EqualTo(RequestAction.modify.ToString()));
                Assert.That(tasks.Any(t => t.TaskType == WfTaskType.access.ToString()), Is.False, "the connection is implemented");
            });
        }

        [Test]
        public async Task DeletedAppServerWithoutRolloutRemoved_NoViewAnalysesTheConnection()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kGroupNameRecogOpt);
            ModellingConnection staleStatus = CreateConnectionWithDeletedAppServer();
            staleStatus.AddProperty(ConState.VarianceChecked.ToString());
            staleStatus.AddProperty(ConState.VarianceFound.ToString());

            OwnerConnectionReport report = await RunReport(CreateConnectionWithDeletedAppServer(), userConfig);
            ModellingConnection status = await RunStatus(staleStatus, userConfig);
            List<WfReqTask> tasks = await RunRequest(CreateConnectionWithDeletedAppServer(), userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(report.ExcludedConnections, Has.Count.EqualTo(1), "the report lists the connection instead of dropping it");
                Assert.That(report.ExcludedConnections[0].Connection.Id, Is.EqualTo(9));
                Assert.That(report.ExcludedConnections[0].Reason, Is.EqualTo(ConState.DeletedObjects));
                Assert.That(report.ModelledConnectionsCount, Is.Zero);
                Assert.That(report.RuleDifferences, Is.Empty);

                Assert.That(status.GetBoolProperty(ConState.VarianceChecked.ToString()), Is.False, "stale variance flags are removed");
                Assert.That(status.GetBoolProperty(ConState.VarianceFound.ToString()), Is.False);
                Assert.That(status.GetBoolProperty(ConState.DeletedObjects.ToString()), Is.True);

                Assert.That(tasks.Where(t => t.GetRuleElements().Count > 0), Is.Empty);
            });
        }

        [Test]
        public async Task SurplusMemberOfResolvedGroup_AllViewsReportTheDifference()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kResolveGroupRecogOpt);

            OwnerConnectionReport report = await RunReport(CreateConnectionWithSurplusGroupMember(), userConfig);
            ModellingConnection status = await RunStatus(CreateConnectionWithSurplusGroupMember(), userConfig);
            List<WfReqTask> tasks = await RunRequest(CreateConnectionWithSurplusGroupMember(), userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(report.RuleDifferences, Has.Count.EqualTo(1), "a production group with an extra host is a difference");
                NetworkObject sourceGroup = report.RuleDifferences[0].ImplementedRules[0].Froms[0].Object;
                Assert.That(sourceGroup.Name, Is.EqualTo("AR504711-003"));
                Assert.That(sourceGroup.IsSurplus, Is.True, "the group holding the surplus host is marked");

                Assert.That(status.GetBoolProperty(ConState.VarianceFound.ToString()), Is.True);
                Assert.That(tasks.Count(t => t.TaskType == WfTaskType.rule_modify.ToString()), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DocumentationOnlyConnection_IsNotComparedInReport()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kGroupNameRecogOpt);
            ModellingConnection documentationOnly = CreateConnectionWithSurplusGroupMember();
            documentationOnly.ExtraConfigs = [new() { ExtraConfigType = GlobalConst.kDoku_ + "Info", ExtraConfigText = "documentation" }];

            OwnerConnectionReport report = await RunReport(documentationOnly, userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(report.ModelledConnectionsCount, Is.Zero);
                Assert.That(report.Connections, Is.Empty, "a documentation-only connection is not 'not implemented'");
                Assert.That(report.RuleDifferences, Is.Empty);
            });
        }

        private static readonly List<string> DifferingRuleUids = [kDifferingRuleUid];
    }
}
