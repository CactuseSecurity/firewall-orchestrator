using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Services;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingVarianceAnalysisTest
    {
        const string stdRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true}";
        const string oppRecogOpt = "{\"nwRegardIp\":false,\"nwRegardName\":true,\"nwRegardGroupName\":true,\"nwResolveGroup\":true,\"nwSeparateGroupAnalysis\":false,\"svcRegardPortAndProt\":false,\"svcRegardName\":true,\"svcRegardGroupName\":true,\"svcResolveGroup\":false}";
        const string grpNameRecogOpt = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":true,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true}";

        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}",
            ModRolloutResolveServiceGroups = true,
            CreateAppZones = true,
            RuleRecognitionOption = stdRecogOpt
        };
        static readonly ModellingVarianceAnalysisTestApiConn varianceAnalysisApiConnection = new();
        static readonly ExtStateTestApiConn extStateApiConnection = new();
        readonly ExtStateHandler extStateHandler = new(extStateApiConnection);
    
        static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };

        static readonly ModellingAppServer AS1 = new() {Id = 1, Name = "AppServerUnchanged", Ip = "1.2.3.4" };
        static readonly ModellingAppServer AS2 = new() {Id = 2, Name = "AppServerNew1/32", Ip = "1.1.1.1", IpEnd = "1.1.1.1" };
        static readonly ModellingAppServer AS3 = new() {Id = 3, Name = "AppServerNew2", Ip = "2.2.2.2", IpEnd = "2.2.2.2" };

        static readonly ModellingAppRole AR1 = new() { Id = 1, Name = "AppRole1", IdString = "AR504711-001", AppServers = [ new(){Content = AS1}, new(){Content = AS2} ]};
        static readonly ModellingAppRole AR2 = new() { Id = 2, Name = "AppRole2", IdString = "AR504711-002", AppServers = [ new(){Content = AS3} ]};
        static readonly ModellingAppRole AR3 = new() { Id = 3, Name = "AppRole3", IdString = "AR504711-003", AppServers = [ new(){Content = AS1} ]};

        static readonly ModellingService Svc1 = new() { Id = 1, Name = "Service1", Port = 1000, PortEnd = 2000, ProtoId = 6 };
        static readonly ModellingService Svc2 = new() { Id = 2, Name = "Service2", Port = 4000, ProtoId = 6 };

        static readonly ModellingServiceGroup SvcGrp1 = new(){ Id = 1, Name = "SvcGrp1", Services = [ new(){ Content = Svc2 } ]};

        static readonly ModellingConnection Connection1 = new()
        {
            Id = 1,
            Name = "Conn1",
            SourceAppRoles = [ new(){ Content = AR1 } ],
            DestinationAppRoles = [ new(){ Content = AR2 } ],
            DestinationAppServers = [ new(){ Content = AS1 } ],
            ServiceGroups = [ new(){ Content = SvcGrp1 } ],
            Services = [ new(){Content = Svc1 } ]
        };
        static readonly ModellingConnection Connection2 = new()
        {
            Id = 2,
            Name = "Conn2",
            SourceAppServers = [ new(){ Content = AS1 } ],
            DestinationAppRoles = [ new(){ Content = AR3 } ],
            Services = [ new(){Content = Svc1 } ]
        };
        static readonly ModellingConnection Connection3 = new()
        {
            Id = 3,
            Name = "Conn3",
            Services = [ new(){Content = Svc2 } ]
        };
        static readonly ModellingConnection Connection4 = new()
        {
            Id = 4,
            Name = "Conn4",
            SourceAppRoles = [ new(){ Content = AR1 } ],
            SourceAppServers = [ new(){ Content = AS1 } ],
            DestinationAppRoles = [ new(){ Content = AR3 } ],
            Services = [ new(){Content = Svc1 } ],
            ExtraConfigs = [ new(){ ExtraConfigType = "IDA_user", ExtraConfigText = "SpecObj1" },
                            new(){ ExtraConfigType = "IDA_user", ExtraConfigText = "SpecObj2" },
                            new(){ ExtraConfigType = "IDA_user", ExtraConfigText = "SpecObj3" } ]
        };



        [SetUp]
        public void Initialize()
        {
        }

        [Test]
        public async Task TestAnalyseModelledConnectionsForRequest()
        {
            List<ModellingConnection> Connections = [ Connection1 ];
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            List<WfReqTask> TaskList = await varianceAnalysis.AnalyseModelledConnectionsForRequest(Connections);

            ClassicAssert.AreEqual(6, TaskList.Count);

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AZ4711\",\"AppRoleId\":\"3\"}", TaskList[0].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[0].RequestAction);
            ClassicAssert.AreEqual(1, TaskList[0].TaskNumber);
            ClassicAssert.AreEqual("Update AppZone: AZ4711: Add Members", TaskList[0].Title);
            ClassicAssert.AreEqual(4, TaskList[0].Elements.Count);
            ClassicAssert.AreEqual("AppServerNew1_32", TaskList[0].Elements[0].Name);
            ClassicAssert.AreEqual("AppServerNew2", TaskList[0].Elements[1].Name);
            ClassicAssert.AreEqual("AppServerUnchanged", TaskList[0].Elements[2].Name);
            ClassicAssert.AreEqual("AZ4711", TaskList[0].Elements[0].GroupName);
            ClassicAssert.AreEqual("create", TaskList[0].Elements[0].RequestAction);
            ClassicAssert.AreEqual("1.1.1.1/32", TaskList[0].Elements[0].IpString);
            ClassicAssert.AreEqual("1.1.1.1/32", TaskList[0].Elements[0].IpEnd);
            ClassicAssert.AreEqual("create", TaskList[0].Elements[1].RequestAction);
            ClassicAssert.AreEqual("2.2.2.2/32", TaskList[0].Elements[1].IpString);
            ClassicAssert.AreEqual("2.2.2.2/32", TaskList[0].Elements[1].IpEnd);
            ClassicAssert.AreEqual("source", TaskList[0].Elements[0].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[0].Elements[2].RequestAction);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[0].Elements[2].IpString);
            ClassicAssert.AreEqual("unchanged", TaskList[0].Elements[3].RequestAction);
            ClassicAssert.AreEqual("1.0.0.0", TaskList[0].Elements[3].IpString);

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-001\",\"AppRoleId\":\"1\"}", TaskList[1].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[1].RequestAction);
            ClassicAssert.AreEqual(2, TaskList[1].TaskNumber);
            ClassicAssert.AreEqual("Update AppRole: AR504711-001: Add Members", TaskList[1].Title);
            ClassicAssert.AreEqual(1, TaskList[1].ManagementId);
            ClassicAssert.AreEqual("Checkpoint1", TaskList[1].OnManagement?.Name);
            ClassicAssert.AreEqual(3, TaskList[1].Elements.Count);
            ClassicAssert.AreEqual("AppServerNew1/32", TaskList[1].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[0].GroupName);
            ClassicAssert.AreEqual("1.1.1.1", TaskList[1].Elements[0].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[0].Field);
            ClassicAssert.AreEqual("addAfterCreation", TaskList[1].Elements[0].RequestAction);
            ClassicAssert.AreEqual("AppServerUnchanged", TaskList[1].Elements[1].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[1].GroupName);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[1].Elements[1].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[1].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[1].Elements[1].RequestAction);
            ClassicAssert.AreEqual("AppServerOld", TaskList[1].Elements[2].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[2].GroupName);
            ClassicAssert.AreEqual("1.0.0.0", TaskList[1].Elements[2].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[2].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[1].Elements[2].RequestAction);

            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-002\",\"AppRoleId\":\"2\"}", TaskList[2].AdditionalInfo);
            ClassicAssert.AreEqual("create", TaskList[2].RequestAction);
            ClassicAssert.AreEqual(3, TaskList[2].TaskNumber);
            ClassicAssert.AreEqual("New AppRole: AR504711-002", TaskList[2].Title);
            ClassicAssert.AreEqual(1, TaskList[2].Elements.Count);
            ClassicAssert.AreEqual("AppServerNew2", TaskList[2].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-002", TaskList[2].Elements[0].GroupName);
            ClassicAssert.AreEqual("2.2.2.2", TaskList[2].Elements[0].IpString);
            ClassicAssert.AreEqual("2.2.2.2", TaskList[2].Elements[0].IpEnd);
            ClassicAssert.AreEqual("source", TaskList[2].Elements[0].Field);
            ClassicAssert.AreEqual("addAfterCreation", TaskList[2].Elements[0].RequestAction);

            ClassicAssert.AreEqual(WfTaskType.access.ToString(), TaskList[3].TaskType);
            ClassicAssert.AreEqual("{\"ConnId\":\"1\"}", TaskList[3].AdditionalInfo);
            ClassicAssert.AreEqual("create", TaskList[3].RequestAction);
            ClassicAssert.AreEqual(4, TaskList[3].TaskNumber);
            ClassicAssert.AreEqual("New Connection: Conn1", TaskList[3].Title);
            ClassicAssert.AreEqual(1, TaskList[3].Owners.Count);
            ClassicAssert.AreEqual("App1", TaskList[3].Owners[0].Owner.Name);
            ClassicAssert.AreEqual(5, TaskList[3].Elements.Count);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[3].Elements[0].GroupName);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[0].IpString);
            ClassicAssert.AreEqual("source", TaskList[3].Elements[0].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[0].RequestAction);

            ClassicAssert.AreEqual(null, TaskList[3].Elements[1].Name);
            ClassicAssert.AreEqual("AR504711-002", TaskList[3].Elements[1].GroupName);
            ClassicAssert.AreEqual("destination", TaskList[3].Elements[1].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[1].RequestAction);

            ClassicAssert.AreEqual("AppServerUnchanged", TaskList[3].Elements[2].Name);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[2].GroupName);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[3].Elements[2].IpString);
            ClassicAssert.AreEqual("destination", TaskList[3].Elements[2].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[2].RequestAction);

            ClassicAssert.AreEqual("Service2", TaskList[3].Elements[3].Name);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[3].GroupName);
            ClassicAssert.AreEqual(6, TaskList[3].Elements[3].ProtoId);
            ClassicAssert.AreEqual(4000, TaskList[3].Elements[3].Port);
            ClassicAssert.AreEqual("service", TaskList[3].Elements[3].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[3].RequestAction);
            ClassicAssert.AreEqual("Service1", TaskList[3].Elements[4].Name);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[4].GroupName);
            ClassicAssert.AreEqual(6, TaskList[3].Elements[4].ProtoId);
            ClassicAssert.AreEqual(1000, TaskList[3].Elements[4].Port);
            ClassicAssert.AreEqual(2000, TaskList[3].Elements[4].PortEnd);
            ClassicAssert.AreEqual("service", TaskList[3].Elements[4].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[4].RequestAction);

            ClassicAssert.AreEqual("{\"GrpName\":\"AZ4711\",\"AppRoleId\":\"3\"}", TaskList[4].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[4].RequestAction);
            ClassicAssert.AreEqual(4, TaskList[4].Elements.Count);
            ClassicAssert.AreEqual("AppServerOld", TaskList[4].Elements[0].Name);
            ClassicAssert.AreEqual("delete", TaskList[4].Elements[0].RequestAction);
            ClassicAssert.AreEqual("AppServerUnchanged", TaskList[4].Elements[1].Name);
            ClassicAssert.AreEqual("unchanged", TaskList[4].Elements[1].RequestAction);
            ClassicAssert.AreEqual("AppServerNew1_32", TaskList[4].Elements[2].Name);
            ClassicAssert.AreEqual("unchanged", TaskList[4].Elements[2].RequestAction);
            ClassicAssert.AreEqual("AppServerNew2", TaskList[4].Elements[3].Name);

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[5].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-001\",\"AppRoleId\":\"1\"}", TaskList[5].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[5].RequestAction);
            ClassicAssert.AreEqual(6, TaskList[5].TaskNumber);
            ClassicAssert.AreEqual("Update AppRole: AR504711-001: Remove Members", TaskList[5].Title);
            ClassicAssert.AreEqual(1, TaskList[5].ManagementId);
            ClassicAssert.AreEqual("Checkpoint1", TaskList[5].OnManagement?.Name);
            ClassicAssert.AreEqual(3, TaskList[5].Elements.Count);
            ClassicAssert.AreEqual("AppServerOld", TaskList[5].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[5].Elements[0].GroupName);
            ClassicAssert.AreEqual("1.0.0.0", TaskList[5].Elements[0].IpString);
            ClassicAssert.AreEqual("source", TaskList[5].Elements[0].Field);
            ClassicAssert.AreEqual("delete", TaskList[5].Elements[0].RequestAction);
            ClassicAssert.AreEqual("AppServerUnchanged", TaskList[5].Elements[1].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[5].Elements[1].GroupName);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[5].Elements[1].IpString);
            ClassicAssert.AreEqual("source", TaskList[5].Elements[1].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[5].Elements[1].RequestAction);
            ClassicAssert.AreEqual("AppServerNew1/32", TaskList[5].Elements[2].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[5].Elements[2].GroupName);
            ClassicAssert.AreEqual("1.1.1.1", TaskList[5].Elements[2].IpString);
            ClassicAssert.AreEqual("source", TaskList[5].Elements[2].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[5].Elements[2].RequestAction);
        }

        [Test]
        public async Task TestAnalyseModelledConnectionsForRequestWithServiceGroups()
        {
            List<ModellingConnection> Connections = [ Connection1 ];
            userConfig.ModRolloutResolveServiceGroups = false;
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            List<WfReqTask> TaskList = await varianceAnalysis.AnalyseModelledConnectionsForRequest(Connections);

            ClassicAssert.AreEqual(7, TaskList.Count);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[3].TaskType);
            ClassicAssert.AreEqual(WfTaskType.access.ToString(), TaskList[4].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[5].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[6].TaskType);

            ClassicAssert.AreEqual("{\"GrpName\":\"SvcGrp1\",\"SvcGrpId\":\"1\"}", TaskList[3].AdditionalInfo);
            ClassicAssert.AreEqual("create", TaskList[3].RequestAction);
            ClassicAssert.AreEqual(4, TaskList[3].TaskNumber);
            ClassicAssert.AreEqual("New Servicegroup: SvcGrp1", TaskList[3].Title);
            ClassicAssert.AreEqual(1, TaskList[3].Elements.Count);
            ClassicAssert.AreEqual("Service2", TaskList[3].Elements[0].Name);
            ClassicAssert.AreEqual("SvcGrp1", TaskList[3].Elements[0].GroupName);
            ClassicAssert.AreEqual(6, TaskList[3].Elements[0].ProtoId);
            ClassicAssert.AreEqual(4000, TaskList[3].Elements[0].Port);
            ClassicAssert.AreEqual("service", TaskList[3].Elements[0].Field);
            ClassicAssert.AreEqual("create", TaskList[3].Elements[0].RequestAction);
            userConfig.ModRolloutResolveServiceGroups = true;
        }

        [Test]
        public async Task TestAnalyseRules()
        {
            // open: NA, Enabled, Negated, DropRule

            List<ModellingConnection> Connections = [ Connection1, Connection2, Connection3 ];
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            ModellingFilter modellingFilter = new();
            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections(Connections, modellingFilter);

            ClassicAssert.AreEqual(1, result.UnModelledRules.Count);
            ClassicAssert.AreEqual(1, result.UnModelledRules[1].Count);
            ClassicAssert.AreEqual("NonModelledRule", result.UnModelledRules[1].First().Name);

            ClassicAssert.AreEqual(1, result.ConnsNotImplemented.Count);
            ClassicAssert.AreEqual(3, result.ConnsNotImplemented[0].Id);
            ClassicAssert.AreEqual("Conn3", result.ConnsNotImplemented[0].Name);

            ClassicAssert.AreEqual(1, result.RuleDifferences.Count);
            ClassicAssert.AreEqual("Conn1", result.RuleDifferences[0].ModelledConnection.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules.Count);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms.Length);
            ClassicAssert.AreEqual("AppRole1 (AR504711-001)", result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms[0].Object.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].DisregardedTos.Length);
            ClassicAssert.AreEqual("AppServerUnchanged", result.RuleDifferences[0].ImplementedRules[0].DisregardedTos[0].Object.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].DisregardedServices.Length);
            ClassicAssert.AreEqual("Service2", result.RuleDifferences[0].ImplementedRules[0].DisregardedServices[0].Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Froms.Length);
            ClassicAssert.AreEqual("AppServerOld", result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.Name);
            ClassicAssert.AreEqual(true, result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Tos.Length);
            ClassicAssert.AreEqual("AR504711-001", result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Services.Length);
            ClassicAssert.AreEqual("Service1", result.RuleDifferences[0].ImplementedRules[0].Services[0].Content.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Services[0].Content.IsSurplus);

            ClassicAssert.AreEqual(1, result.DifferingAppRoles.Count);
            ClassicAssert.AreEqual(1, result.DifferingAppRoles[1].Count);
            ClassicAssert.AreEqual("AR504711-001", result.DifferingAppRoles[1].First().IdString);
            ClassicAssert.AreEqual(1, result.DifferingAppRoles[1].First().SurplusAppServers.Count);
            ClassicAssert.AreEqual("AppServerOld", result.DifferingAppRoles[1].First().SurplusAppServers[0].Content.Name);
            ClassicAssert.AreEqual("1.0.0.0", result.DifferingAppRoles[1].First().SurplusAppServers[0].Content.Ip);
            ClassicAssert.AreEqual(2, result.DifferingAppRoles[1].First().AppServers.Count);
            ClassicAssert.AreEqual("AppServerUnchanged", result.DifferingAppRoles[1].First().AppServers[0].Content.Name);
            ClassicAssert.AreEqual("1.2.3.4", result.DifferingAppRoles[1].First().AppServers[0].Content.Ip);
            ClassicAssert.AreEqual(false, result.DifferingAppRoles[1].First().AppServers[0].Content.NotImplemented);
            ClassicAssert.AreEqual("AppServerNew1/32", result.DifferingAppRoles[1].First().AppServers[1].Content.Name);
            ClassicAssert.AreEqual(true, result.DifferingAppRoles[1].First().AppServers[1].Content.NotImplemented);
        }

        [Test]
        public async Task TestAnalyseRulesSpecialUserObjects()
        {
            List<ModellingConnection> Connections = [ Connection4 ];
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            ModellingFilter modellingFilter = new();
            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections(Connections, modellingFilter);

            ClassicAssert.AreEqual(0, result.ConnsNotImplemented.Count);
            ClassicAssert.AreEqual(1, result.RuleDifferences.Count);
            ClassicAssert.AreEqual("Conn4", result.RuleDifferences[0].ModelledConnection.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules.Count);
            ClassicAssert.AreEqual(0, result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms.Length);
            ClassicAssert.AreEqual(0, result.RuleDifferences[0].ImplementedRules[0].DisregardedTos.Length);
            ClassicAssert.AreEqual(2, result.RuleDifferences[0].ImplementedRules[0].Froms.Length);
            ClassicAssert.AreEqual("SpecObj1", result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.IsSurplus);
            ClassicAssert.AreEqual("AR504711-001", result.RuleDifferences[0].ImplementedRules[0].Froms[1].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Froms[1].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Tos.Length);
            ClassicAssert.AreEqual("SpecObj2", result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].UnusedSpecialUserObjects.Count);
            ClassicAssert.AreEqual("specobj3", result.RuleDifferences[0].ImplementedRules[0].UnusedSpecialUserObjects.First());

            userConfig.RuleRecognitionOption = grpNameRecogOpt;
            varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            result = await varianceAnalysis.AnalyseRulesVsModelledConnections(Connections, modellingFilter);

            ClassicAssert.AreEqual(0, result.ConnsNotImplemented.Count);
            ClassicAssert.AreEqual(1, result.RuleDifferences.Count);
            ClassicAssert.AreEqual("Conn4", result.RuleDifferences[0].ModelledConnection.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules.Count);
            ClassicAssert.AreEqual(0, result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms.Length);
            ClassicAssert.AreEqual(0, result.RuleDifferences[0].ImplementedRules[0].DisregardedTos.Length);
            ClassicAssert.AreEqual(2, result.RuleDifferences[0].ImplementedRules[0].Froms.Length);
            ClassicAssert.AreEqual("SpecObj1", result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.IsSurplus);
            ClassicAssert.AreEqual("AR504711-001", result.RuleDifferences[0].ImplementedRules[0].Froms[1].Object.Name);
            ClassicAssert.AreEqual(true, result.RuleDifferences[0].ImplementedRules[0].Froms[1].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Tos.Length);
            ClassicAssert.AreEqual("SpecObj2", result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].UnusedSpecialUserObjects.Count);
            ClassicAssert.AreEqual("specobj3", result.RuleDifferences[0].ImplementedRules[0].UnusedSpecialUserObjects.First());

            userConfig.RuleRecognitionOption = stdRecogOpt;
        }

        [Test]
        public async Task TestAnalyseRulesOppositeRecogOptions()
        {
            List<ModellingConnection> Connections = [ Connection1, Connection2, Connection3 ];
            userConfig.RuleRecognitionOption = oppRecogOpt;
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            ModellingFilter modellingFilter = new();
            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections(Connections, modellingFilter);

            ClassicAssert.AreEqual(1, result.UnModelledRules.Count);
            ClassicAssert.AreEqual(1, result.UnModelledRules[1].Count);
            ClassicAssert.AreEqual("NonModelledRule", result.UnModelledRules[1].First().Name);

            ClassicAssert.AreEqual(1, result.ConnsNotImplemented.Count);
            ClassicAssert.AreEqual(3, result.ConnsNotImplemented[0].Id);
            ClassicAssert.AreEqual("Conn3", result.ConnsNotImplemented[0].Name);

            ClassicAssert.AreEqual(1, result.RuleDifferences.Count);
            ClassicAssert.AreEqual("Conn1", result.RuleDifferences[0].ModelledConnection.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules.Count);
            ClassicAssert.AreEqual(2, result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms.Length);
            ClassicAssert.AreEqual("AppServerUnchanged", result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms[0].Object.Name);
            ClassicAssert.AreEqual("AppServerNew1/32", result.RuleDifferences[0].ImplementedRules[0].DisregardedFroms[1].Object.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].DisregardedTos.Length);
            ClassicAssert.AreEqual("AppServerNew2", result.RuleDifferences[0].ImplementedRules[0].DisregardedTos[0].Object.Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].DisregardedServices.Length);
            ClassicAssert.AreEqual("SvcGrp1", result.RuleDifferences[0].ImplementedRules[0].DisregardedServices[0].Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Froms.Length);
            ClassicAssert.AreEqual("AppServerOld", result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.Name);
            ClassicAssert.AreEqual(true, result.RuleDifferences[0].ImplementedRules[0].Froms[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Tos.Length);
            ClassicAssert.AreEqual("AR504711-001", result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Tos[0].Object.IsSurplus);
            ClassicAssert.AreEqual(1, result.RuleDifferences[0].ImplementedRules[0].Services.Length);
            ClassicAssert.AreEqual("Service1", result.RuleDifferences[0].ImplementedRules[0].Services[0].Content.Name);
            ClassicAssert.AreEqual(false, result.RuleDifferences[0].ImplementedRules[0].Services[0].Content.IsSurplus);

            ClassicAssert.AreEqual(0, result.DifferingAppRoles.Count);
            userConfig.RuleRecognitionOption = stdRecogOpt;
        }

        [Test]
        public async Task TestAnalyseRulesOtherMarkerLocation()
        {
            List<ModellingConnection> Connections = [ Connection1, Connection2, Connection3 ];
            userConfig.ModModelledMarker = "XXX";
            userConfig.ModModelledMarkerLocation = "comment";
            ModellingVarianceAnalysis varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
            ModellingFilter modellingFilter = new(){ AnalyseRemainingRules = true };
            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections(Connections, modellingFilter);

            ClassicAssert.AreEqual(2, result.ConnsNotImplemented.Count);
            ClassicAssert.AreEqual("Conn1", result.ConnsNotImplemented[0].Name);
            ClassicAssert.AreEqual("Conn2", result.ConnsNotImplemented[1].Name);
            ClassicAssert.AreEqual(1, result.RuleDifferences.Count);
            ClassicAssert.AreEqual("Conn3", result.RuleDifferences[0].ModelledConnection.Name);
            ClassicAssert.AreEqual(1, result.UnModelledRules.Count);
            ClassicAssert.AreEqual(3, result.UnModelledRules[1].Count);
            ClassicAssert.AreEqual("FWOC1", result.UnModelledRules[1][0].Name);
            ClassicAssert.AreEqual("xxxFWOC2yyy", result.UnModelledRules[1][1].Name);
            userConfig.ModModelledMarker = "FWOC";
            userConfig.ModModelledMarkerLocation = "rulename";
         }
    }
}
