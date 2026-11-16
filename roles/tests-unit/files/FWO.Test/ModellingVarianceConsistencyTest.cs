using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
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
        private const string kGroupNameRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":true,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}";
        private const string kResolveGroupRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":true,\"nwSeparateGroupAnalysis\":false,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}";
        private const string kNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}";

        private const long kDummyAppRoleId = SimulatedVarianceConnections.kDummyAppRoleId;
        private static readonly FwoOwner Owner = SimulatedVarianceConnections.Owner;
        private static readonly ExtStateHandler ExtStates = new(new ExtStateTestApiConn());

        private static SimulatedUserConfig CreateUserConfig(bool rolloutRemoved, string recognitionOption) => new()
        {
            ModNamingConvention = kNamingConvention,
            RuleRecognitionOption = recognitionOption,
            ModRolloutRemovedAppServers = rolloutRemoved,
            CreateAppZones = false
        };

        private static ModellingVarianceAnalysis CreateAnalysis(SimulatedUserConfig userConfig)
        {
            return new(new SimulatedVarianceApiConnection(), ExtStates, userConfig, Owner, DefaultInit.DoNothing);
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

            OwnerConnectionReport report = await RunReport(SimulatedVarianceConnections.WithDeletedAppServer(), userConfig);
            ModellingConnection status = await RunStatus(SimulatedVarianceConnections.WithDeletedAppServer(), userConfig);
            List<WfReqTask> tasks = await RunRequest(SimulatedVarianceConnections.WithDeletedAppServer(), userConfig);

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
                Assert.That(ruleTasks[0].GetRuleElements().Single().RuleUid, Is.EqualTo(SimulatedVarianceApiConnection.kDifferingRuleUid));
                Assert.That(ruleTasks[0].RequestAction, Is.EqualTo(RequestAction.modify.ToString()));
                Assert.That(tasks.Any(t => t.TaskType == WfTaskType.access.ToString()), Is.False, "the connection is implemented");
            });
        }

        [Test]
        public async Task DeletedAppServerWithoutRolloutRemoved_NoViewAnalysesTheConnection()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kGroupNameRecogOpt);
            ModellingConnection staleStatus = SimulatedVarianceConnections.WithDeletedAppServer();
            staleStatus.AddProperty(ConState.VarianceChecked.ToString());
            staleStatus.AddProperty(ConState.VarianceFound.ToString());

            OwnerConnectionReport report = await RunReport(SimulatedVarianceConnections.WithDeletedAppServer(), userConfig);
            ModellingConnection status = await RunStatus(staleStatus, userConfig);
            List<WfReqTask> tasks = await RunRequest(SimulatedVarianceConnections.WithDeletedAppServer(), userConfig);

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

            OwnerConnectionReport report = await RunReport(SimulatedVarianceConnections.WithSurplusGroupMember(), userConfig);
            ModellingConnection status = await RunStatus(SimulatedVarianceConnections.WithSurplusGroupMember(), userConfig);
            List<WfReqTask> tasks = await RunRequest(SimulatedVarianceConnections.WithSurplusGroupMember(), userConfig);

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
        public async Task EqualMembersOfResolvedGroups_NoViewReportsADifference()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kResolveGroupRecogOpt);

            OwnerConnectionReport report = await RunReport(SimulatedVarianceConnections.WithEqualGroupMembers(), userConfig);
            ModellingConnection status = await RunStatus(SimulatedVarianceConnections.WithEqualGroupMembers(), userConfig);
            List<WfReqTask> tasks = await RunRequest(SimulatedVarianceConnections.WithEqualGroupMembers(), userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(report.ExcludedConnections, Is.Empty, "the connection is analysed");
                Assert.That(report.Connections, Is.Empty, "the connection is implemented");
                Assert.That(report.RuleDifferences, Is.Empty, "the own flat entry of a production group is no surplus member");

                Assert.That(status.GetBoolProperty(ConState.VarianceChecked.ToString()), Is.True);
                Assert.That(status.GetBoolProperty(ConState.VarianceFound.ToString()), Is.False);
                Assert.That(status.GetBoolProperty(ConState.NotImplemented.ToString()), Is.False);

                Assert.That(tasks.Count(t => t.TaskType == WfTaskType.rule_modify.ToString()), Is.Zero);
            });
        }

        [Test]
        public async Task DocumentationOnlyConnection_IsNotComparedInReport()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(false, kGroupNameRecogOpt);
            ModellingConnection documentationOnly = SimulatedVarianceConnections.WithSurplusGroupMember();
            documentationOnly.ExtraConfigs = [new() { ExtraConfigType = GlobalConst.kDoku_ + "Info", ExtraConfigText = "documentation" }];

            OwnerConnectionReport report = await RunReport(documentationOnly, userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(report.ModelledConnectionsCount, Is.Zero);
                Assert.That(report.Connections, Is.Empty, "a documentation-only connection is not 'not implemented'");
                Assert.That(report.RuleDifferences, Is.Empty);
            });
        }

        private static readonly List<string> DifferingRuleUids = [SimulatedVarianceApiConnection.kDifferingRuleUid];
    }
}
