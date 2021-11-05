using System;
using System.Threading.Tasks;
using FWO.ApiClient;
using NUnit.Framework;
using FWO.Api.Data;
using FWO.Config.File;
using FWO.Middleware.Client;
using Microsoft.IdentityModel.Tokens;
using FWO.Middleware.RequestParameters;

namespace FWO.Test.Api
{
    [TestFixture]
    public class ApiTest
    {
        APIConnection apiConnection;

        [SetUp]
        public void EtablishConnectionToServer()
        {
            ConfigFile configConnection = new ConfigFile();
            string ApiUri = configConnection.ApiServerUri;
            string MiddlewareUri = configConnection.MiddlewareServerUri;
            string ProductVersion = configConnection.ProductVersion;
            RsaSecurityKey jwtPublicKey = configConnection.JwtPublicKey;
            string middlewareServerUri = configConnection.MiddlewareServerUri;
            string apiServerUri = configConnection.ApiServerUri;
            MiddlewareClient middlewareClient = new MiddlewareClient(MiddlewareUri);
            AuthenticationTokenGetParameters authenticationParameters = new AuthenticationTokenGetParameters
            {
                Username = "user1_demo",
                Password = "cactus1"
            };
            string jwt = middlewareClient.AuthenticateUser(authenticationParameters).Result.Data;
            apiConnection = new APIConnection(apiServerUri);
            apiConnection.SetAuthHeader(jwt);
            return;
        }

        [Test]
        public async Task QueryTestIpProto()
        {
            string query = @"
                    query ipProtoTest 
                    {
                        stm_ip_proto (where: {ip_proto_id: {_eq: 6 } }) 
                            {
                                id: ip_proto_id
                                name: ip_proto_name
                            }
                    }";

            NetworkProtocol networkProtocol = new NetworkProtocol();
            networkProtocol = (await apiConnection.SendQueryAsync<NetworkProtocol[]>(query, new {}))[0];
            Assert.AreEqual(networkProtocol.Name, "TCP", "wrong result of protocol API query");
        }
    }
}
