using Microsoft.IdentityModel.Tokens;
using System;
using FWO.Logging;
using FWO.Config;
using FWO.Auth.Client;
using FWO.ApiClient;
using FWO.ApiConfig.Data;
using FWO.ApiClient.Queries;

namespace FWO.ApiConfig
{
    public class ConfigCollection
    {
        /// <summary>
        /// Internal connection to auth server. Used to connect with api server.
        /// </summary>
        private readonly AuthClient authClient;

        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        private readonly APIConnection apiConnection;

        private readonly string productVersion;
        private readonly UiText[] uiTexts;

        public ConfigCollection(string jwt)
        {
            ConfigConnection config = new Config.ConfigConnection();
            RsaSecurityKey jwtPublicKey = config.JwtPublicKey;
            string authServerUri = config.AuthServerUri;
            string apiServerUri = config.ApiServerUri;
            productVersion = config.ProductVersion;
            
            try
            {
                authClient = new AuthClient(authServerUri);
                apiConnection = new APIConnection(apiServerUri);
                apiConnection.SetAuthHeader(jwt);
                uiTexts = apiConnection.SendQueryAsync<UiText>(BasicQueries.getUiTexts).Result;
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server .", exception);
                Environment.Exit(1); // Exit with error
            }
        }
    }
}
