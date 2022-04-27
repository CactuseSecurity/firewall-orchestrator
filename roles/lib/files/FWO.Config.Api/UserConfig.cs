using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.Config.Api.Data;
using FWO.ApiClient;
using FWO.Api.Data;
using FWO.ApiClient.Queries;
using System.Reflection;
using System.Text.Json.Serialization;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfig : Config
    {
        private readonly GlobalConfig globalConfig;

        public Dictionary<string, string> Translate { get; set; }

        public UiUser User { private set; get; }

        public static async Task<UserConfig> ConstructAsync(GlobalConfig globalConfig, APIConnection apiConnection, int userId)
        {
            UiUser[] users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = userId });
            UiUser? user = users.FirstOrDefault();
            if (user == null)
            {
                Log.WriteError("Load user config", $"User with id {userId} could not be found in database.");
                throw new Exception();
            }
            return new UserConfig(globalConfig, apiConnection, user);
        }

        public UserConfig(GlobalConfig globalConfig, APIConnection apiConnection, UiUser user) : base(apiConnection, user.DbId)
        {
            User = user;
            Translate = globalConfig.langDict[user.Language!];
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfigOnChange;
        }

        public UserConfig(GlobalConfig globalConfig) : base()
        {
            User = new UiUser();
            Translate = globalConfig.langDict[globalConfig.DefaultLanguage];
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfigOnChange;
        }

        private void GlobalConfigOnChange(Config config, ConfigItem[] changedItems)
        {
            // Get properties that belong to the user config 
            IEnumerable<PropertyInfo> properties = GetType().GetProperties()
                .Where(prop => prop.CustomAttributes.Any(attr => attr.GetType() == typeof(UserConfigDataAttribute)));

            // Exclude all properties from update that belong to the user config
            ConfigItem[] relevantChangedItems = changedItems.Where(configItem =>
                !properties.Any(prop => ((JsonPropertyNameAttribute)prop.GetCustomAttribute(typeof(JsonPropertyNameAttribute))!).Name == configItem.Key)).ToArray();

            Update(relevantChangedItems);
            InvokeOnChange(this, changedItems);
        }

        public async Task SetUserInformation(string userDn, APIConnection apiConnection)
        {
            GlobalConfigOnChange(globalConfig, globalConfig.RawConfigItems);
            Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            UiUser[]? users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn });
            if (users.Count() > 0)
                User = users[0];
            await SetUserId(apiConnection, User.DbId);

            if (User.Language == null)
            {
                User.Language = DefaultLanguage;
            }
            await ChangeLanguage(User.Language, apiConnection);
        }

        public async Task ChangeLanguage(string languageName, APIConnection apiConnection)
        {
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserLanguage, new { id = User.DbId, language = languageName });
            Translate = globalConfig.langDict[languageName];
            User.Language = languageName;
            InvokeOnChange(this, null);
        }

        public string GetUserLanguage()
        {
            return (User.Language != null ? User.Language : "");
        }

        public void SetLanguage(string languageName)
        {
            User = new UiUser() { Language = globalConfig.DefaultLanguage };
            if (languageName != null && languageName != "")
            {
                User.Language = languageName;
            }
            if (globalConfig.langDict.ContainsKey(User.Language))
            {
                Translate = globalConfig.langDict[User.Language];
            }
        }

        public override string GetText(string key)
        {
            if (Translate.ContainsKey(key))
            {
                return Convert(Translate[key]);
            }
            else
            {
                string defaultLanguage = globalConfig.DefaultLanguage;
                if (defaultLanguage == "")
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
            if (User != null && User.Language != null)
            {
                string startLink = "<a href=\"/";
                string insertString = $"/?lang={User.Language}";

                int begin, end;
                int index = 0;
                bool cont = true;

                while (cont)
                {
                    begin = plainText.IndexOf(startLink, index);
                    if (begin > 0)
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
                string msg = GetText(key.Substring(0, 5));
                if (msg != "(undefined text)")
                {
                    text = msg;
                }
            }
            return text;
        }
    }
}
