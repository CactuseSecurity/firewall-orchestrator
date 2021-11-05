﻿﻿using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using FWO.Logging;
using FWO.Config.File;
using FWO.ApiClient;
using FWO.Config.Api.Data;
using FWO.ApiClient.Queries;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all publicly available config data needed for the UI server at startup
    /// </summary>
    public class GlobalConfig
    {
        /// <summary>
        /// Internal connection to auth server. Used to connect with api server.
        /// </summary>
        //private readonly MiddlewareClient authClient;

        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        private readonly APIConnection apiConnection;

        /// <summary>
        /// Global string constants used e.g. as database keys etc.
        /// </summary>
        public static readonly string kDefaultLanguage = "DefaultLanguage";
        public static readonly string kEnglish = "English";
        public static readonly string kElementsPerFetch = "elementsPerFetch";
        public static readonly string kMaxInitialFetchesRightSidebar = "maxInitialFetchesRightSidebar";
        public static readonly string kAutoFillRightSidebar = "autoFillRightSidebar";
        public static readonly string kDataRetentionTime = "dataRetentionTime";
        public static readonly string kImportSleepTime = "importSleepTime";
        public static readonly string kRecertificationPeriod = "recertificationPeriod";
        public static readonly string kRecertificationNoticePeriod = "recertificationNoticePeriod";
        public static readonly string kRecertificationDisplayPeriod = "recertificationDisplayPeriod";
        public static readonly string kRuleRemovalGracePeriod = "ruleRemovalGracePeriod";
        public static readonly string kCommentRequired = "commentRequired";
        public static readonly string kPwMinLength = "pwMinLength";
        public static readonly string kPwUpperCaseRequired = "pwUpperCaseRequired";
        public static readonly string kPwLowerCaseRequired = "pwLowerCaseRequired";
        public static readonly string kPwNumberRequired = "pwNumberRequired";
        public static readonly string kPwSpecialCharactersRequired = "pwSpecialCharactersRequired";

        public string productVersion { get; set; }

        public UiText[] uiTexts { get; set; }
        public Language[] uiLanguages { get; set; }

        public Dictionary<string, Dictionary<string, string>> langDict { get; set; }
        public string defaultLanguage { get; set; }

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
        public GlobalConfig(string jwt)
        {
            ConfigFile config = new ConfigFile();
            productVersion = config.ProductVersion;

            //authClient = new MiddlewareClient(config.MiddlewareServerUri);
            apiConnection = new APIConnection(config.ApiServerUri);
            apiConnection.SetAuthHeader(jwt);
            
            ConfigDbAccess configTable = new ConfigDbAccess(apiConnection);
            try
            {
                defaultLanguage = configTable.Get<string>(kDefaultLanguage);
            }
            catch(Exception exception)
            {
                Log.WriteError("Read Config table", $"Key not found: taking English ", exception);
                defaultLanguage = kEnglish;
            }

            // get languages defined 
            try
            {
                uiLanguages = apiConnection.SendQueryAsync<Language[]>(ConfigQueries.getLanguages).Result;
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
                    uiTexts = apiConnection.SendQueryAsync<UiText[]>(ConfigQueries.getTextsPerLanguage, languageVariable).Result;
                    foreach (UiText text in uiTexts)
                        dict.Add(text.Id, text.Txt); // add "word" to dictionary

                    // add language dictionary to dictionary of dictionaries
                    langDict.Add(lang.Name, dict);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("ApiConfig connection", $"Could not connect to API server.", exception);
                Environment.Exit(1); // Exit with error
            }
        }

        public static string ShowBool(bool boolVal)
        {
            return (boolVal ? "\u2714" : "\u2716");
        }
    }
}
