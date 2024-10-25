using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Data;
using FWO.Services;

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
        static readonly ModellingAppServer AS2 = new() {Id = 1, Name = "AppServer2", Ip = "1.2.3.5", IpEnd = "1.2.3.10" };

        static readonly ModellingAppRole AR1 = new() { Id = 1, AppServers = [ new(){Content = AS1} ]};
        static readonly ModellingAppRole AR2 = new() { Id = 1, AppServers = [ new(){Content = AS2} ]};

        static readonly ModellingService Svc1 = new() { Id = 1, Name = "Service1", Port = 1000, PortEnd = 2000, ProtoId = 6 };

        static readonly ModellingConnection Connection1 = new()
        {
            Id = 1,
            Name = "Conn1",
            SourceAppRoles = [ new(){ Content = AR1 } ],
            DestinationAppRoles = [ new(){ Content = AR2 } ],
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

            ClassicAssert.AreEqual(2, TaskList.Count);
        }
    }
}
