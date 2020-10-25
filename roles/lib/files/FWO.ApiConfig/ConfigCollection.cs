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

        public Dictionary<string, Dictionary<string, string>> langDict { get; set; }
        public string defaultLanguage { get; set; }

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
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
            defaultLanguage = "English";

            // get languages defined 
            try
            {
                uiLanguages = apiConnection.SendQueryAsync<Language>(BasicQueries.getLanguages).Result;
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server to get languages.", exception);
                Environment.Exit(1); // Exit with error
            }

            langDict = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                foreach (Language lang in uiLanguages)
                {
                    var languageVariable = new { language = lang.Name };
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    uiTexts = apiConnection.SendQueryAsync<UiText>(BasicQueries.getTextsPerLanguage, languageVariable).Result;
                    foreach (UiText text in uiTexts)
                        dict.Add(text.Id, text.Txt); // add "word" to dictionary

                    // add language dictionary to dictionary of dictionaries
                    langDict.Add(lang.Name, dict);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server .", exception);
                Environment.Exit(1); // Exit with error
            }
        }
    }
}
