using FWO.Logging;
using FWO.Config.File;
using FWO.Basics;
using FWO.Api.Client;
using FWO.Config.Api.Data;
using FWO.Api.Client.Queries;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all publicly available config data needed for the UI server at startup
    /// </summary>
    public class GlobalConfig : Config
    {
        /// <summary>
        /// Global config constants
        /// </summary>
        public string ProductVersion { get; set; }
        public Language[] UiLanguages { get; set; }
        public Dictionary<string, Dictionary<string, string>> LangDict { get; set; }
        public Dictionary<string, Dictionary<string, string>> OverDict { get; set; }


        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
        public static async Task<GlobalConfig> ConstructAsync(string jwt, bool loadLanguageData = true, bool withSubscription = false)
        {
            ApiConnection apiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
            return await ConstructAsync(apiConnection, loadLanguageData, withSubscription);
        }
        
        public static async Task<GlobalConfig> ConstructAsync(ApiConnection apiConnection, bool loadLanguageData = true, bool withSubscription = false)
        {
            string productVersion = ConfigFile.ProductVersion;

            Language[] uiLanguages = [];
            Dictionary<string, Dictionary<string, string>> tmpLangDicts = [];
            Dictionary<string, Dictionary<string, string>> tmpLangOverDicts = [];

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
                    // add language dictionaries to dictionary of dictionaries
                    foreach (Language lang in uiLanguages)
                    {
                        tmpLangDicts.Add(lang.Name, await LoadLangDict(lang, apiConnection));
                        tmpLangOverDicts.Add(lang.Name, await LoadLangDict(lang, apiConnection, true));
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("ApiConfig connection", $"Could not connect to API server.", exception);
                    Environment.Exit(1); // Exit with error
                }
            }

            return new GlobalConfig(apiConnection, productVersion, uiLanguages, tmpLangDicts, tmpLangOverDicts, withSubscription);
        }

        private GlobalConfig(ApiConnection apiConnection, string productVersion, Language[] uiLanguages,
                Dictionary<string, Dictionary<string, string>> langDict, Dictionary<string, Dictionary<string, string>> overDict, bool withSubscription = false)
            : base(apiConnection, 0, withSubscription)
        {
            ProductVersion = productVersion;
            UiLanguages = uiLanguages;
            LangDict = langDict;
            OverDict = overDict;
        }

        public override string GetText(string key) 
        {
            if(LangDict.TryGetValue(DefaultLanguage, out Dictionary<string, string>? langDict) && langDict.TryGetValue(key, out string? value))
            {
                return System.Web.HttpUtility.HtmlDecode(value);
            }
            return GlobalConst.kUndefinedText;
        }
        
        private static async Task<Dictionary<string, string>> LoadLangDict(Language lang, ApiConnection apiConnection, bool over = false)
        {
            var languageVariable = new { language = lang.Name };
            Dictionary<string, string> dict = [];
            List<UiText> uiTexts = await apiConnection.SendQueryAsync<List<UiText>>(over ? ConfigQueries.getCustomTextsPerLanguage : ConfigQueries.getTextsPerLanguage, languageVariable);
            foreach (UiText text in uiTexts)
            {
                dict.Add(text.Id, text.Txt); // add "word" to dictionary
            }
            return dict;
        }
    }
}
