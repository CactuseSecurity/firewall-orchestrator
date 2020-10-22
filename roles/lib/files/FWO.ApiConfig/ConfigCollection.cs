using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using FWO.Logging;
using FWO.Config;
using FWO.Auth.Client;
using FWO.ApiClient;
using FWO.ApiConfig.Data;
using FWO.ApiClient.Queries;

namespace FWO.ApiConfig
{
    /// <summary>
    /// Collection of all publicly available config data needed for the UI server at startup
    /// </summary>
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

        public string productVersion { get; set; }
        public UiText[] uiTexts { get; set; }
        public Language[] uiLanguages { get; set; }
        public ConfigCollection(string jwt)
        {
            ConfigConnection config = new Config.ConfigConnection();
            RsaSecurityKey jwtPublicKey = config.JwtPublicKey;
            string authServerUri = config.AuthServerUri;
            string apiServerUri = config.ApiServerUri;
            productVersion = config.ProductVersion;
            authClient = new AuthClient(authServerUri);
            apiConnection = new APIConnection(apiServerUri);
            apiConnection.SetAuthHeader(jwt);

            // get languages defined 
            try
            {
                uiLanguages = apiConnection.SendQueryAsync<Language[]>(BasicQueries.getLanguages).Result;
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server to get languages.", exception);
                Environment.Exit(1); // Exit with error
            }

            try
            {
                uiTexts = apiConnection.SendQueryAsync<UiText[]>(BasicQueries.getUiTexts).Result;
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server .", exception);
                Environment.Exit(1); // Exit with error
            }

            // // prepare array of dictionaries containing the texts for each language
            // Dictionary<string,LocalizedText>[] languageTexts = new Dictionary<string,LocalizedText>[uiLanguages.Length];

            // try
            // {
            //     uiTexts = apiConnection.SendQueryAsync<UiText>(BasicQueries.getUiTexts).Result;
            //     foreach (UiText text in uiTexts)
            //     {
            //         string[] Languages = {}; 
            //         if (UiText.Id == "German") 
            //             = networkServices.Concat(management.Services).ToArray();
            //     }
            // }
            // catch (Exception exception)
            // {
            //     Log.WriteError("ApiConfig connection", $"Could not connect to API server .", exception);
            //     Environment.Exit(1); // Exit with error
            // }
        }
    }
}
