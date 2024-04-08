using FWO.Logging;
using FWO.Config.File;
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
        public string productVersion { get; set; }
        public Language[] uiLanguages { get; set; }
        public Dictionary<string, Dictionary<string, string>> langDict { get; set; }
        public Dictionary<string, Dictionary<string, string>> overDict { get; set; }


        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users)
        /// </summary>
        public static async Task<GlobalConfig> ConstructAsync(string jwt, bool loadLanguageData = true)
        {
            ApiConnection apiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
            return await ConstructAsync(apiConnection, loadLanguageData);
        }
        
        public static async Task<GlobalConfig> ConstructAsync(ApiConnection apiConnection, bool loadLanguageData = true)
        {
            string productVersion = ConfigFile.ProductVersion;

            Language[] uiLanguages = Array.Empty<Language>();
            Dictionary<string, Dictionary<string, string>> tmpLangDicts = new();
            Dictionary<string, Dictionary<string, string>> tmpLangOverDicts = new();

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

            return new GlobalConfig(apiConnection, productVersion, uiLanguages, tmpLangDicts, tmpLangOverDicts);
        }

        private GlobalConfig(ApiConnection apiConnection, string productVersion, Language[] uiLanguages,
                Dictionary<string, Dictionary<string, string>> langDict, Dictionary<string, Dictionary<string, string>> overDict)
            : base(apiConnection, 0)
        {
            this.productVersion = productVersion;
            this.uiLanguages = uiLanguages;
            this.langDict = langDict;
            this.overDict = overDict;
        }

        public override string GetText(string key) 
        {
            if(langDict.ContainsKey(DefaultLanguage) && langDict[DefaultLanguage].ContainsKey(key))
            {
                return System.Web.HttpUtility.HtmlDecode(langDict[DefaultLanguage][key]);
            }
            return "(undefined text)";
        }
        
        private static async Task<Dictionary<string, string>> LoadLangDict(Language lang, ApiConnection apiConnection, bool over = false)
        {
            var languageVariable = new { language = lang.Name };
            Dictionary<string, string> dict = new();
            List<UiText> uiTexts = await apiConnection.SendQueryAsync<List<UiText>>(over ? ConfigQueries.getCustomTextsPerLanguage : ConfigQueries.getTextsPerLanguage, languageVariable);
            foreach (UiText text in uiTexts)
            {
                dict.Add(text.Id, text.Txt); // add "word" to dictionary
            }
            return dict;
        }
    }
}
