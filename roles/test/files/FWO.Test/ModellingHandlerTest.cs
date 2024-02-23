using NUnit.Framework;
using FWO.Api.Data;
using FWO.Ui.Services;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingHandlerTest
    {
        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}"
        };
        static readonly ModellingHandlerTestApiConn apiConnection = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new();
        static readonly List<KeyValuePair<int, long>> AvailableNwElems = new();
        static readonly List<ModellingAppRole> AvailableAppRoles = new();
        static readonly ModellingAppRole AppRole = new();
        static readonly bool AddAppRoleMode = false;
        static readonly bool IsOwner = true;

        static readonly ModellingAppServer AppServerInside1 = new(){ Name = "AppServerInside1", Ip = "10.0.0.0" };
        static readonly ModellingAppServer AppServerInside2 = new(){ Name = "AppServerInside2", Ip = "10.0.0.5" };
        static readonly ModellingAppServer AppServerInside3 = new(){ Name = "AppServerInside3", Ip = "11.0.0.1" };
        static readonly List<ModellingAppServer> AvailableAppServers = new() 
        { 
            AppServerInside1,
            AppServerInside2,
            AppServerInside3,
            new(){ Ip = "1.0.0.0" },
            new(){ Ip = "10.1.0.0" },
            new(){ Ip = "11.0.0.4" },
            new(){ Ip = "12.0.0.0" },
            new(){ Ip = "255.255.255.255" }
        };

        static readonly ModellingNetworkArea TestArea = new(){ Name = "Area1", IdString = "NA50", Subnets = new()
        { 
            new(){ Content = new(){ Name = "Testsubnet1", Ip = "10.0.0.0/24", IpEnd = "10.0.0.0/24" }},
            new(){ Content = new(){ Name = "Testsubnet2", Ip = "11.0.0.0/30", IpEnd = "11.0.0.0/30" }}
        }};

        static readonly ModellingNamingConvention NamingConvention1 = new()
        {
            NetworkAreaRequired = true, UseAppPart = false, FixedPartLength = 4, FreePartLength = 5, NetworkAreaPattern = "NA", AppRolePattern = "AR"
        };
        static readonly ModellingNamingConvention NamingConvention2 = new()
        {
            NetworkAreaRequired = true, UseAppPart = true, FixedPartLength = 4, FreePartLength = 3, NetworkAreaPattern = "NA", AppRolePattern = "AR"
        };

        ModellingAppRoleHandler? AppRoleHandler;
        ModellingAppHandler? AppHandler;


        [SetUp]
        public void Initialize()
        {
            AppHandler = new (apiConnection, userConfig, Application, DisplayMessageInUi, IsOwner);
            AppRoleHandler = new (apiConnection, userConfig, Application, AvailableAppRoles, AppRole,
                AvailableAppServers, AvailableNwElems, AddAppRoleMode, DisplayMessageInUi, IsOwner);
        }


        // HandlerBase
        [Test]
        public async Task TestExtractUsedSrcInterface()
        {
            ModellingConnection conn = new(){ Id = 3, UsedInterfaceId = 1 };
            Assert.AreEqual("Interf1", await AppHandler.ExtractUsedInterface(conn));
            Assert.AreEqual(true, conn.SrcFromInterface);
            Assert.AreEqual(false, conn.DstFromInterface);
            Assert.AreEqual(0, conn.SourceAppServers.Count);
            Assert.AreEqual("AppRole1", conn.SourceAppRoles[0].Content.Name);
            Assert.AreEqual("NwGroup1", conn.SourceNwGroups[0].Content.Name);
            Assert.AreEqual(0, conn.DestinationAppServers.Count);
            Assert.AreEqual(0, conn.DestinationAppRoles.Count);
            Assert.AreEqual(0, conn.DestinationNwGroups.Count);
            Assert.AreEqual("ServiceGrp1", conn.ServiceGroups[0].Content.Name);
            Assert.AreEqual(0, conn.Services.Count);
        }

        [Test]
        public async Task TestExtractUsedDstInterface()
        {
            ModellingConnection conn = new(){ Id = 4, UsedInterfaceId = 2 };
            Assert.AreEqual("Interf2", await AppHandler.ExtractUsedInterface(conn));
            Assert.AreEqual(false, conn.SrcFromInterface);
            Assert.AreEqual(true, conn.DstFromInterface);
            Assert.AreEqual(0, conn.SourceAppServers.Count);
            Assert.AreEqual(0, conn.SourceAppRoles.Count);
            Assert.AreEqual(0, conn.SourceNwGroups.Count);
            Assert.AreEqual("AppServer2", conn.DestinationAppServers[0].Content.Name);
            Assert.AreEqual("AppRole2", conn.DestinationAppRoles[0].Content.Name);
            Assert.AreEqual(0, conn.DestinationNwGroups.Count);
            Assert.AreEqual(0, conn.ServiceGroups.Count);
            Assert.AreEqual("Service2", conn.Services[0].Content.Name);
        }

        // AppHandler
        [Test]
        public void TestGetSrcDstSvcNames()
        {
            ModellingConnection conn = new()
            {
                UsedInterfaceId = 1,
                SrcFromInterface = false,
                SourceAppServers = new(){ new(){ Content = AppServerInside1 }, new(){ Content = AppServerInside2 }},
                SourceAppRoles = new(){ new(){ Content = new(){ Name = "AppRole1", IdString = "AR5000001", IsDeleted = true }}},
                SourceNwGroups = new(){ new(){ Content = TestArea }},
                DstFromInterface = true,
                DestinationAppServers = new(){ new(){ Content = AppServerInside3 }},
                DestinationAppRoles = new(){},
                DestinationNwGroups = new(){},
                ServiceGroups = new(){ new(){ Content = new(){ Name = "SvcGroup1", IsGlobal = true}}},
                Services = new(){ new(){ Content = new(){ Name = "Svc1", Port = 1111, Protocol = new(){ Name = "UDP"}} }}
            };
            List<string> expectedSrc = new(){"<span class=\"\"><span class=\"oi oi-folder\"></span> <span><b><span class=\"\" ><span class=\"\">Area1 (NA50)</span></span></b></span></span>",
                                             "<span class=\"\"><span class=\"oi oi-list-rich\"></span> <span><b><span class=\"text-danger\" ><i><span class=\"\">!AppRole1 (AR5000001)</span></i></span></b></span></span>",
                                             "<span class=\"\"><span class=\"oi oi-laptop\"></span> <span class=\"\" ><span class=\"\" ><span class=\"\">AppServerInside1 (10.0.0.0)</span></span></span></span>",
                                             "<span class=\"\"><span class=\"oi oi-laptop\"></span> <span class=\"\" ><span class=\"\" ><span class=\"\">AppServerInside2 (10.0.0.5)</span></span></span></span>"};
            List<string> expectedDst = new(){"<span class=\"text-secondary\"><span class=\"oi oi-laptop\"></span> <span class=\"\" ><span class=\"\" ><span class=\"\">AppServerInside3 (11.0.0.1)</span></span></span></span>"};
            List<string> expectedSvc = new(){"<span class=\"text-secondary\"><span class=\"oi oi-list-rich\"></span> <span><b>SvcGroup1</b></span></span>",
                                             "<span class=\"text-secondary\"><span class=\"oi oi-wrench\"></span> <span>Svc1 (1111/UDP)</span></span>"};
            Assert.AreEqual(expectedSrc, AppHandler.GetSrcNames(conn));
            Assert.AreEqual(expectedDst, AppHandler.GetDstNames(conn));
            Assert.AreEqual(expectedSvc, AppHandler.GetSvcNames(conn));
        }


        // AppRoleHandler
        [Test]
        public async Task TestSelectAppServersFromArea()
        {
            List<ModellingAppServer> expectedResult = new() 
            { 
                new(AppServerInside1) { TooltipText = userConfig.GetText("C9002") },
                new(AppServerInside2) { TooltipText = userConfig.GetText("C9002") },
                new(AppServerInside3) { TooltipText = userConfig.GetText("C9002") }
            };
            await AppRoleHandler.SelectAppServersFromArea(TestArea);
            Assert.AreEqual(expectedResult, AppRoleHandler.AppServersInArea);
        }

        [Test]
        public async Task TestProposeFreeAppRoleNumber()
        {
            ModellingManagedIdString idFixString = new() { NamingConvention = NamingConvention1 };
            idFixString.ConvertAreaToAppRoleFixedPart(TestArea.IdString);
            idFixString.SetAppPartFromExtId("APP-1234");
            Assert.AreEqual("00002", await AppRoleHandler.ProposeFreeAppRoleNumber(idFixString));

            idFixString.NamingConvention = NamingConvention2;
            idFixString.ConvertAreaToAppRoleFixedPart("NA91");
            idFixString.SetAppPartFromExtId("APP-1234");
            AppRoleHandler.NamingConvention = NamingConvention2;
            Assert.AreEqual("003", await AppRoleHandler.ProposeFreeAppRoleNumber(idFixString));

            idFixString.ConvertAreaToAppRoleFixedPart("NA99");
            Assert.AreEqual("001", await AppRoleHandler.ProposeFreeAppRoleNumber(idFixString));
        }
    }
}
