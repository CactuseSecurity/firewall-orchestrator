using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Data;
using FWO.Services;
using System.Text;
using FWO.Api.Client.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingVarianceAnalysisTest
    {
        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}",
            ModRolloutResolveServiceGroups = true,
            CreateAppZones = true,
        };
        static readonly ModellingVarianceAnalysisTestApiConn varianceAnalysisApiConnection = new();
        static readonly ExtStateTestApiConn extStateApiConnection = new();
        static readonly ModellingAppZoneHandlerTestApiCon AppZoneHandlerTestApiCon = new();
        readonly ExtStateHandler extStateHandler = new(extStateApiConnection);
        ModellingVarianceAnalysis varianceAnalysis;
        ModellingAppZoneHandler AppZoneHandler;

        static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };

        static readonly ModellingAppServer AS1 = new() {Id = 1, Name = "AppServer1", Ip = "1.2.3.4" };
        static readonly ModellingAppServer AS2 = new() {Id = 2, Name = "AppServer2", Ip = "1.2.3.5", IpEnd = "1.2.3.10" };
        static readonly ModellingAppServer AS3 = new() {Id = 3, Name = "AppServerNew", Ip = "10.10.10.10", IpEnd = "10.10.10.20" };
        static readonly ModellingAppServer AS4 = new() {Id = 4, Name = "AppServer4", Ip = "100.2.3.4" };

        static readonly ModellingAppRole AR1 = new() { Id = 1, Name = "AppRole1", IdString = "AR504711-001", AppServers = [ new(){Content = AS1}, new(){Content = AS3} ]};
        static readonly ModellingAppRole AR2 = new() { Id = 2, Name = "AppRole2", IdString = "AR504711-002", AppServers = [ new(){Content = AS2} ]};

        static readonly ModellingService Svc1 = new() { Id = 1, Name = "Service1", Port = 1000, PortEnd = 2000, ProtoId = 6 };
        static readonly ModellingService Svc2 = new() { Id = 2, Name = "Service2", Port = 4000, ProtoId = 6 };

        static readonly ModellingServiceGroup SvcGrp1 = new(){ Id = 1, Name = "SvcGrp1", Services = [ new(){ Content = Svc2 } ]};

        static readonly ModellingConnection Connection1 = new()
        {
            Id = 1,
            Name = "Conn1",
            SourceAppRoles = [ new(){ Content = AR1 } ],
            DestinationAppRoles = [ new(){ Content = AR2 } ],
            DestinationAppServers = [ new(){ Content = AS4 } ],
            ServiceGroups = [ new(){ Content = SvcGrp1 } ],
            Services = [ new(){Content = Svc1 } ]
        };

        static readonly List<ModellingConnection> Connections = [ Connection1 ];


        [SetUp]
        public void Initialize()
        {
            extStateHandler.Init().Wait();
            varianceAnalysis = new (varianceAnalysisApiConnection, extStateHandler, userConfig, Application, DefaultInit.DoNothing);
        }

        [Test]
        public async Task TestAnalyseModelledConnections()
        {
            List<WfReqTask> TaskList = await varianceAnalysis.AnalyseModelledConnections(Connections);

            ClassicAssert.AreEqual(5, TaskList.Count);

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AZ4711\",\"AppRoleId\":\"3\"}", TaskList[0].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[0].RequestAction);
            ClassicAssert.AreEqual(1, TaskList[0].TaskNumber);
            ClassicAssert.AreEqual("Update AppZone: AZ4711: Add Members", TaskList[0].Title);
            ClassicAssert.AreEqual(2, TaskList[0].Elements.Count);
            ClassicAssert.AreEqual("AppServer2", TaskList[0].Elements[0].Name);
            ClassicAssert.AreEqual("AppServer1", TaskList[0].Elements[1].Name);
            ClassicAssert.AreEqual("AZ4711", TaskList[0].Elements[0].GroupName);
            ClassicAssert.AreEqual("1.1.1.1/32", TaskList[0].Elements[1].IpString);
            ClassicAssert.AreEqual("1.1.1.1/32", TaskList[0].Elements[1].IpEnd);
            ClassicAssert.AreEqual("2.2.2.2/32", TaskList[0].Elements[0].IpString);
            ClassicAssert.AreEqual("2.2.2.2/32", TaskList[0].Elements[0].IpEnd);
            ClassicAssert.AreEqual("source", TaskList[0].Elements[0].Field);
            ClassicAssert.AreEqual("addAfterCreation", TaskList[0].Elements[0].RequestAction);

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-001\",\"AppRoleId\":\"1\"}", TaskList[1].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[1].RequestAction);
            ClassicAssert.AreEqual(2, TaskList[1].TaskNumber);
            ClassicAssert.AreEqual("Update AppRole: AR504711-001: Add Members", TaskList[1].Title);
            ClassicAssert.AreEqual(1, TaskList[1].ManagementId);
            ClassicAssert.AreEqual("Checkpoint1", TaskList[1].OnManagement?.Name);
            ClassicAssert.AreEqual(3, TaskList[1].Elements.Count);
            ClassicAssert.AreEqual("AppServerNew", TaskList[1].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[0].GroupName);
            ClassicAssert.AreEqual("10.10.10.10", TaskList[1].Elements[0].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[0].Field);
            ClassicAssert.AreEqual("create", TaskList[1].Elements[0].RequestAction);
            ClassicAssert.AreEqual("AppServer1", TaskList[1].Elements[1].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[1].GroupName);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[1].Elements[1].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[1].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[1].Elements[1].RequestAction);
            ClassicAssert.AreEqual("AppServer3", TaskList[1].Elements[2].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[1].Elements[2].GroupName);
            ClassicAssert.AreEqual("1.2.4.0/24", TaskList[1].Elements[2].IpString);
            ClassicAssert.AreEqual("source", TaskList[1].Elements[2].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[1].Elements[2].RequestAction);

            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-002\",\"AppRoleId\":\"2\"}", TaskList[2].AdditionalInfo);
            ClassicAssert.AreEqual("create", TaskList[2].RequestAction);
            ClassicAssert.AreEqual(3, TaskList[2].TaskNumber);
            ClassicAssert.AreEqual("New AppRole: AR504711-002", TaskList[2].Title);
            ClassicAssert.AreEqual(1, TaskList[2].Elements.Count);
            ClassicAssert.AreEqual("AppServer2", TaskList[2].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-002", TaskList[2].Elements[0].GroupName);
            ClassicAssert.AreEqual("1.2.3.5", TaskList[2].Elements[0].IpString);
            ClassicAssert.AreEqual("1.2.3.10", TaskList[2].Elements[0].IpEnd);
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

            ClassicAssert.AreEqual("AppServer4", TaskList[3].Elements[2].Name);
            ClassicAssert.AreEqual(null, TaskList[3].Elements[2].GroupName);
            ClassicAssert.AreEqual("100.2.3.4", TaskList[3].Elements[2].IpString);
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

            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[4].TaskType);
            ClassicAssert.AreEqual("{\"GrpName\":\"AR504711-001\",\"AppRoleId\":\"1\"}", TaskList[4].AdditionalInfo);
            ClassicAssert.AreEqual("modify", TaskList[4].RequestAction);
            ClassicAssert.AreEqual(5, TaskList[4].TaskNumber);
            ClassicAssert.AreEqual("Update AppRole: AR504711-001: Remove Members", TaskList[4].Title);
            ClassicAssert.AreEqual(1, TaskList[4].ManagementId);
            ClassicAssert.AreEqual("Checkpoint1", TaskList[4].OnManagement?.Name);
            ClassicAssert.AreEqual(3, TaskList[4].Elements.Count);
            ClassicAssert.AreEqual("AppServer3", TaskList[4].Elements[0].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[4].Elements[0].GroupName);
            ClassicAssert.AreEqual("1.2.4.0/24", TaskList[4].Elements[0].IpString);
            ClassicAssert.AreEqual("source", TaskList[4].Elements[0].Field);
            ClassicAssert.AreEqual("delete", TaskList[4].Elements[0].RequestAction);
            ClassicAssert.AreEqual("AppServer1", TaskList[4].Elements[1].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[4].Elements[1].GroupName);
            ClassicAssert.AreEqual("1.2.3.4", TaskList[4].Elements[1].IpString);
            ClassicAssert.AreEqual("source", TaskList[4].Elements[1].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[4].Elements[1].RequestAction);
            ClassicAssert.AreEqual("AppServerNew", TaskList[4].Elements[2].Name);
            ClassicAssert.AreEqual("AR504711-001", TaskList[4].Elements[2].GroupName);
            ClassicAssert.AreEqual("10.10.10.10", TaskList[4].Elements[2].IpString);
            ClassicAssert.AreEqual("source", TaskList[4].Elements[2].Field);
            ClassicAssert.AreEqual("unchanged", TaskList[4].Elements[2].RequestAction);

            userConfig.ModRolloutResolveServiceGroups = false;
            TaskList = await varianceAnalysis.AnalyseModelledConnections(Connections);
            ClassicAssert.AreEqual(6, TaskList.Count);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[3].TaskType);
            ClassicAssert.AreEqual(WfTaskType.access.ToString(), TaskList[4].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[5].TaskType);

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

        }
    }
}
