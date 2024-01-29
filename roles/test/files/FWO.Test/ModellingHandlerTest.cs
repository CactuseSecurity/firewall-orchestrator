using NUnit.Framework;
using FWO.Api.Data;
using FWO.Ui.Services;
using FWO.Api.Client.Queries;
using GraphQL;

namespace FWO.Test
{
    internal class ModHandlerTestApiConn : SimulatedApiConnection
    {
        const string AppRoleId1 = "AR5000001";
        ModellingAppRole AppRole1 = new(){ Id = 1, IdString = AppRoleId1 };


        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<ModellingAppRole>))
            {
                List<ModellingAppRole>? appRoles = new();
                if(query == ModellingQueries.getNewestAppRoles)
                {
                    string pattern = variables.GetType().GetProperties().First(o => o.Name == "pattern").GetValue(variables , null).ToString();
                    if(variables != null && (pattern == AppRoleId1 || pattern == "AR50%"))
                    {
                        appRoles = new(){ AppRole1 };
                    }
                }
                else
                {
                    appRoles = new(){ AppRole1 };
                }
                
                GraphQLResponse<dynamic> response = new(){ Data = appRoles };
                return response.Data;
            }
            throw new NotImplementedException();
        }
    }

    [TestFixture]
    [Parallelizable]
    internal class ModellingHandlerTest
    {
        static readonly SimulatedUserConfig userConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}"
        };
        static readonly ModHandlerTestApiConn apiConnection = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new();
        static readonly List<KeyValuePair<int, long>> AvailableNwElems = new();
        static readonly List<ModellingAppRole> AvailableAppRoles = new();
        static readonly ModellingAppRole AppRole = new();
        static readonly bool AddAppRoleMode = false;
        static readonly bool IsOwner = true;

        static readonly ModellingAppServer AppServerInside1 = new(){ Ip = "10.0.0.5" };
        static readonly ModellingAppServer AppServerOutside1 = new(){ Ip = "10.1.0.0" };
        static readonly ModellingAppServer AppServerInside2 = new(){ Ip = "11.0.0.1" };
        static readonly ModellingAppServer AppServerOutside2 = new(){ Ip = "11.0.0.4" };
        static readonly List<ModellingAppServer> AvailableAppServers = new() { AppServerInside1, AppServerOutside1, AppServerInside2, AppServerOutside2 };

        static readonly ModellingNetworkArea? TestArea = new(){ IdString = "NA50", Subnets = new()
        { 
            new(){ Content = new(){ Name = "Testsubnet1", Ip = "10.0.0.0/24", IpEnd = "10.0.0.0/24" }},
            new(){ Content = new(){ Name = "Testsubnet2", Ip = "11.0.0.0/30", IpEnd = "11.0.0.0/30" }}
        }};

        ModellingAppRoleHandler? AppRoleHandler;


        [SetUp]
        public void Initialize()
        {
            AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles,
                    AppRole, AvailableAppServers, AvailableNwElems, AddAppRoleMode, DisplayMessageInUi, IsOwner);
        }


        [Test]
        public async Task TestSelectAppServersFromArea()
        {
            List<ModellingAppServer> expectedResult = new() 
            { 
                new(AppServerInside1) { TooltipText = userConfig.GetText("C9002") },
                new(AppServerInside2) { TooltipText = userConfig.GetText("C9002") }
            };
            await AppRoleHandler.SelectAppServersFromArea(TestArea);
            Assert.AreEqual(expectedResult, AppRoleHandler.AppServersInArea);
        }

        [Test]
        public async Task TestProposeFreeAppRoleNumber()
        {
            Assert.AreEqual("00002", await AppRoleHandler.ProposeFreeAppRoleNumber(TestArea));
        }

        [Test]
        public async Task TestReconstructAreaIdString()
        {
            Assert.AreEqual("NA50", AppRoleHandler.ReconstructAreaIdString(new(){ Id = 1, IdString = "AR5000001" }));
        }
    }
}
