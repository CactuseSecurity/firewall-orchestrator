using System;
using System.Collections.Generic;
using FWO.Logging;
using FWO.Config.File;
using FWO.ApiClient;
using FWO.Config.Api.Data;
using FWO.ApiClient.Queries;
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
        //public static readonly string kDefaultLanguage = "defaultLanguage";
        public static readonly string kEnglish = "English";
        //public static readonly string kElementsPerFetch = "elementsPerFetch";
        //public static readonly string kMaxInitialFetchesRightSidebar = "maxInitialFetchesRightSidebar";
        //public static readonly string kAutoFillRightSidebar = "autoFillRightSidebar";
        //public static readonly string kDataRetentionTime = "dataRetentionTime";
        //public static readonly string kImportSleepTime = "importSleepTime";
        //public static readonly string kDailyCheckStartAt = "dailyCheckStartAt";
        //public static readonly string kAutoDiscoverSleepTime = "autoDiscoverSleepTime";
        //public static readonly string kAutoDiscoverStartAt = "autoDiscoverStartAt";
        //public static readonly string kFwApiElementsPerFetch = "fwApiElementsPerFetch";
        //public static readonly string kRecertificationPeriod = "recertificationPeriod";
        //public static readonly string kRecertificationNoticePeriod = "recertificationNoticePeriod";
        //public static readonly string kRecertificationDisplayPeriod = "recertificationDisplayPeriod";
        //public static readonly string kRuleRemovalGracePeriod = "ruleRemovalGracePeriod";
        //public static readonly string kCommentRequired = "commentRequired";
        //public static readonly string kPwMinLength = "pwMinLength";
        //public static readonly string kPwUpperCaseRequired = "pwUpperCaseRequired";
        //public static readonly string kPwLowerCaseRequired = "pwLowerCaseRequired";
        //public static readonly string kPwNumberRequired = "pwNumberRequired";
        //public static readonly string kPwSpecialCharactersRequired = "pwSpecialCharactersRequired";
        //public static readonly string kMinCollapseAllDevices = "minCollapseAllDevices";
        //public static readonly string kMessageViewTime = "messageViewTime";

        //public static readonly int kDefaultInitElementsPerFetch = 100;
        //public static readonly int kDefaultInitMaxInitFetch = 10;
        //public static readonly int kDefaultInitMessageViewTime = 7;
        //public static readonly int kDefaultInitDataRetentionTime = 731;
        //public static readonly int kDefaultInitImportSleepTime = 40;
        //public static readonly int kDefaultInitAutoDiscoverSleepTime = 24;
        //public static readonly int kDefaultInitFwApiElementsPerFetch = 150;
        //public static readonly int kDefaultInitRecertificationPeriod = 365;
        //public static readonly int kDefaultInitRecertificationNoticePeriod = 30;
        //public static readonly int kDefaultInitRecertificationDisplayPeriod = 30;
        //public static readonly int kDefaultInitRuleRemovalGracePeriod = 60;
        //public static readonly int kDefaultInitCollapseFrom = 15;
        //public static readonly int kDefaultInitMinPwdLength = 10;

        public static readonly int kSidebarLeftWidth = 300;
        public static readonly int kSidebarRightWidth = 300;

        public static readonly string kAutodiscovery = "autodiscovery";
        public static readonly string kDailyCheck = "dailycheck";
        public static readonly string kUi = "ui";
    
        public string productVersion { get; set; }

        public Language[] uiLanguages { get; set; }
        public Dictionary<string, Dictionary<string, string>> langDict { get; set; }

        public static async Task<GlobalConfig> ConstructAsync(APIConnection apiConnection, bool loadLanguageData = true, ConfigFile? configFile = null)
        {
            ConfigFile config = configFile ?? new ConfigFile();
            string productVersion = config.ProductVersion;

            Language[] uiLanguages = Array.Empty<Language>();
            Dictionary<string, Dictionary<string, string>> langDict = new Dictionary<string, Dictionary<string, string>>();

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
                        Dictionary<string, string> dict = new Dictionary<string, string>();
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
            ConfigFile config = new ConfigFile();
            APIConnection apiConnection = new APIConnection(config.ApiServerUri);
            apiConnection.SetAuthHeader(jwt);
            return await ConstructAsync(apiConnection, loadLanguageData, config);
        }

        public override string GetText(string key) => langDict[DefaultLanguage][key];


        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
        private GlobalConfig(APIConnection apiConnection, string productVersion, Language[] uiLanguages, Dictionary<string, Dictionary<string, string>> langDict)
                : base(apiConnection, 0)
        {
            this.productVersion = productVersion;
            this.uiLanguages = uiLanguages;
            this.langDict = langDict;
        }
    }
}
