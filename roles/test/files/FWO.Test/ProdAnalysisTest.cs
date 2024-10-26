using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Data;
using FWO.Services;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ProdAnalysis
    {
        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}",
            ModRolloutResolveServiceGroups = true
        };
        static readonly ProdAnalysisTestApiConn prodAnalysisApiConnection = new();
        static readonly ExtStateTestApiConn extStateApiConnection = new();
        readonly ExtStateHandler extStateHandler = new(extStateApiConnection);
        ModellingProdAnalysis prodAnalysis;

        static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };

        static readonly ModellingAppServer AS1 = new() {Id = 1, Name = "AppServer1", Ip = "1.2.3.4" };
        static readonly ModellingAppServer AS2 = new() {Id = 2, Name = "AppServer2", Ip = "1.2.3.5", IpEnd = "1.2.3.10" };
        static readonly ModellingAppServer AS3 = new() {Id = 3, Name = "AppServerNew", Ip = "10.10.10.10", IpEnd = "10.10.10.20" };
        static readonly ModellingAppServer AS4 = new() {Id = 4, Name = "AppServer4", Ip = "100.2.3.4" };

        static readonly ModellingAppRole AR1 = new() { Id = 1, Name = "AppRole1", IdString = "AR504711-001", AppServers = [ new(){Content = AS1}, new(){Content = AS3} ]};
        static readonly ModellingAppRole AR2 = new() { Id = 2, Name = "AppRole2", AppServers = [ new(){Content = AS2} ]};

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
            extStateHandler.Init();
            prodAnalysis = new (prodAnalysisApiConnection, extStateHandler, userConfig);
        }

        [Test]
        public async Task TestAnalyseModelledConnections()
        {
            List<WfReqTask> TaskList = await prodAnalysis.AnalyseModelledConnections(Connections, Application);

            ClassicAssert.AreEqual(4, TaskList.Count);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual(WfTaskType.access.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[3].TaskType);


            userConfig.ModRolloutResolveServiceGroups = false;
            TaskList = await prodAnalysis.AnalyseModelledConnections(Connections, Application);
            ClassicAssert.AreEqual(5, TaskList.Count);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[0].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[1].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), TaskList[2].TaskType);
            ClassicAssert.AreEqual(WfTaskType.access.ToString(), TaskList[3].TaskType);
            ClassicAssert.AreEqual(WfTaskType.group_modify.ToString(), TaskList[4].TaskType);
        }
    }
}
