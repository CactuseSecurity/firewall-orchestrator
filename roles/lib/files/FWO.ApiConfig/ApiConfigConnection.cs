using Microsoft.IdentityModel.Tokens;
using System;
using FWO.Logging;
using FWO.Config;
using FWO.Auth.Client;
using FWO.ApiClient;

namespace FWO.ApiConfig
{
    public class ApiConfigConnection
    {
        /// <summary>
        /// Internal connection to auth server. Used to connect with api server.
        /// </summary>
        private readonly AuthClient authClient;

        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        private readonly APIConnection apiConnection;

        public ApiConfigConnection(string jwt)
        {
            ConfigConnection config = new Config.ConfigConnection();
            RsaSecurityKey jwtPublicKey = config.JwtPublicKey;
            string authServerUri = config.AuthServerUri;
            string apiServerUri = config.ApiServerUri;

            try
            {
                authClient = new AuthClient(authServerUri);
                apiConnection = new APIConnection(apiServerUri);
                apiConnection.SetAuthHeader(jwt);
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server .", exception);
                Environment.Exit(1); // Exit with error
            }
        }
    }
}