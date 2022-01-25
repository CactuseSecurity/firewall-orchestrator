using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.Config.Api.Data;
using FWO.ApiClient;
using FWO.Api.Data;
using FWO.ApiClient.Queries;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfig
    {
        private readonly GlobalConfig globalConfig;

        private Dictionary<string, string> userConfigItems = new Dictionary<string, string>();
        private Dictionary<string, string> defaultConfigItems = new Dictionary<string, string>();

        public Dictionary<string, string> Translate {get; set; }

        public UiUser User { private set; get; }

        public event Func<UserConfig, Task>? OnChange;

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users
        /// </summary>
        /// 
        public UserConfig(GlobalConfig globalConfigIn)
        {
            Translate = globalConfigIn.langDict[globalConfigIn.defaultLanguage];
            globalConfig = globalConfigIn;
            User = new UiUser();
        }

        public async Task SetUserInformation(string userDn, APIConnection apiConnection)
        {
            Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            UiUser[]? users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn });
            if (users.Count() > 0)
                User = users[0];

            defaultConfigItems = await GetConfigItems(0, apiConnection);
            userConfigItems = await GetConfigItems(User.DbId, apiConnection);
            
            if(User.Language == null)
            {
                User.Language = GetConfigValue(GlobalConfig.kDefaultLanguage);
            }
            await ChangeLanguage(User.Language, apiConnection);
        }

        public async Task ReloadDefaults(APIConnection apiConnection)
        {
            defaultConfigItems = await GetConfigItems(0, apiConnection);
        }

        private async Task<Dictionary<string, string>> GetConfigItems(int userId, APIConnection apiConnection)
        {
            ConfigItem[] apiConfItems = await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, new { user = userId });
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (ConfigItem confItem in apiConfItems)
            {
                result.Add(confItem.Key, (confItem.Value != null ? confItem.Value : ""));
            }

            return result;
        }

        public async Task ChangeLanguage(string languageName, APIConnection apiConnection)
        {
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserLanguage, new { id = User.DbId, language = languageName });

            Translate = globalConfig.langDict[languageName];
            if (OnChange != null)
                await OnChange.Invoke(this);
        }

        public string GetUserLanguage()
        {
            return (User.Language != null ? User.Language : "");
        }

        public void SetLanguage(string languageName)
        {
            User = new UiUser(){Language = globalConfig.defaultLanguage};
            if(languageName != null && languageName != "")
            {
                User.Language = languageName;
            }
            if (globalConfig.langDict.ContainsKey(User.Language))
            {
                Translate = globalConfig.langDict[User.Language];
            }
        }

        public string GetText(string key)
        {
            if(Translate.ContainsKey(key))
            {
                return Convert(Translate[key]);
            }
            else 
            {
                string defaultLanguage = GetConfigValue(GlobalConfig.kDefaultLanguage);
                if(defaultLanguage == "")
                {
                    defaultLanguage = GlobalConfig.kEnglish;
                }
                if (globalConfig.langDict[defaultLanguage].ContainsKey(key))
                {
                    return Convert(globalConfig.langDict[defaultLanguage][key]);
                }
                else if (defaultLanguage != GlobalConfig.kEnglish && globalConfig.langDict[GlobalConfig.kEnglish].ContainsKey(key))
                {
                    return Convert(globalConfig.langDict[GlobalConfig.kEnglish][key]);
                }
                else
                {
                    return "(undefined text)";
                }
            }
        }

        public string Convert(string rawText)
        {
            string plainText = System.Web.HttpUtility.HtmlDecode(rawText);

            // Heuristic to add language parameter to internal links
            if(User != null && User.Language != null)
            {
                string startLink = "<a href=\"/";
                string insertString = $"/?lang={User.Language}";

                int begin, end;
                int index = 0;
                bool cont = true;

                while(cont)
                {
                    begin = plainText.IndexOf(startLink, index);
                    if(begin > 0)
                    {
                        end = plainText.IndexOf("\"", begin + startLink.Length);
                        if (end > 0)
                        {
                            plainText = plainText.Insert(end, insertString);
                            index = end + insertString.Length;
                        }
                        else
                        {
                            cont = false;
                        }
                    }
                    else
                    {
                        cont = false;
                    }
                }
            }
            return plainText;
        }

        public string GetApiText(string key)
        {
            string text = key;
            string pattern = @"[A]\d\d\d\d";
            Match m = Regex.Match(key, pattern);
            if (m.Success)
            {
                string msg = GetText(key.Substring(0,5));
                if (msg != "(undefined text)")
                {
                    text = msg;
                }
            }
            return text;
        }

        public string GetConfigValue(string key)
        {
            string settingsValue = "";
            if (userConfigItems.ContainsKey(key))
            {
                settingsValue = userConfigItems[key];
            }
            else if (defaultConfigItems.ContainsKey(key))
            {
                settingsValue = defaultConfigItems[key];
            }
            return settingsValue;
        }

        public async Task ChangeConfigValue(string key, string value, APIConnection apiConnection)
        {
            var apiVariables = new { key, value, user = User.DbId };

            try
            {
                if (userConfigItems.ContainsKey(key))
                {
                    await apiConnection.SendQueryAsync<object>(ConfigQueries.updateConfigItem, apiVariables);
                    userConfigItems[key] = value;
                }
                else
                {
                    await apiConnection.SendQueryAsync<object>(ConfigQueries.addConfigItem, apiVariables);
                    userConfigItems.Add(key, value);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Write Config", $"Could not write key: {key}, user: {User.Dn}, value: {value} to config: ", exception);
                throw;
            }
        }
    }
}
