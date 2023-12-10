using System;
using System.Collections.Generic;
using FWO.Logging;
using FWO.Config.File;
using FWO.Api.Client;
using FWO.Config.Api.Data;
using FWO.Api.Client.Queries;
using System.ComponentModel;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all publicly available config data needed for the UI server at startup
    /// </summary>
    public class GlobalConfig : Config
    {
        /// <summary>
        /// Global string constants used e.g. as database keys etc.
        /// </summary>
        public static readonly string kEnglish = "English";

        public static readonly int kSidebarLeftWidth = 300;
        public static readonly int kSidebarRightWidth = 300;

        public static readonly string kAutodiscovery = "autodiscovery";
        public static readonly string kDailyCheck = "dailycheck";
        public static readonly string kUi = "ui";
        public static readonly string kImportAppData = "importAppData";
        public static readonly string kImportAreaSubnetData = "importAreaSubnetData";
        public static readonly string kManual = "manual";
        public static readonly string kModellerGroup = "ModellerGroup_";
    
        public string productVersion { get; set; }

        public Language[] uiLanguages { get; set; }
        public Dictionary<string, Dictionary<string, string>> langDict { get; set; }

        public static async Task<GlobalConfig> ConstructAsync(ApiConnection apiConnection, bool loadLanguageData = true)
        {
            string productVersion = ConfigFile.ProductVersion;

            Language[] uiLanguages = Array.Empty<Language>();
            Dictionary<string, Dictionary<string, string>> langDict = new();

            if (loadLanguageData)
            {
                // get languages defined 
                try
                {
                    uiLanguages = await apiConnection.SendQueryAsync<Language[]>(ConfigQueries.getLanguages);
                }
                catch (Exception exception)
                {
                    Log.WriteError("ApiConfig connection", $"Could not connect to API server to get languages.", exception);
                    Environment.Exit(1); // Exit with error
                }
                try
                {
                    foreach (Language lang in uiLanguages)
                    {
                        var languageVariable = new { language = lang.Name };
                        Dictionary<string, string> dict = new();
                        UiText[] uiTexts = await apiConnection.SendQueryAsync<UiText[]>(ConfigQueries.getTextsPerLanguage, languageVariable);
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

            return new GlobalConfig(apiConnection, productVersion, uiLanguages, langDict);
        }

        public static async Task<GlobalConfig> ConstructAsync(string jwt, bool loadLanguageData = true)
        {
            ApiConnection apiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
            return await ConstructAsync(apiConnection, loadLanguageData);
        }

        public override string GetText(string key) 
        {
            if(langDict.ContainsKey(DefaultLanguage) && langDict[DefaultLanguage].ContainsKey(key))
            {
                return System.Web.HttpUtility.HtmlDecode(langDict[DefaultLanguage][key]);
            }
            return "(undefined text)";
        } 


        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
        private GlobalConfig(ApiConnection apiConnection, string productVersion, Language[] uiLanguages, Dictionary<string, Dictionary<string, string>> langDict)
                : base(apiConnection, 0)
        {
            this.productVersion = productVersion;
            this.uiLanguages = uiLanguages;
            this.langDict = langDict;
        }
    }
}
