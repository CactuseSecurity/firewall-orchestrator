using System.Text.RegularExpressions;
using FWO.GlobalConstants;
using FWO.Logging;
using FWO.Config.Api.Data;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Api.Client.Queries;
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
        public Dictionary<string, string> Overwrite { get; set; } = [];

        public UiUser User { private set; get; }

        public static async Task<UserConfig> ConstructAsync(GlobalConfig globalConfig, ApiConnection apiConnection, int userId)
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

        public UserConfig(GlobalConfig globalConfig, ApiConnection apiConnection, UiUser user) : base(apiConnection, user.DbId)
        {
            User = user;
            Translate = globalConfig.LangDict[user.Language!];
            Overwrite = apiConnection != null ? Task.Run(async () => await GetCustomDict(user.Language!)).Result : globalConfig.OverDict[user.Language!];
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfigOnChange;
        }

        public UserConfig(GlobalConfig globalConfig) : base()
        {
            User = new UiUser();
            Translate = globalConfig.LangDict[globalConfig.DefaultLanguage];
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfigOnChange;
        }

        // only for unit tests
        protected UserConfig() : base()
        {}
        
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

        public async Task SetUserInformation(string userDn, ApiConnection apiConnection)
        {
            GlobalConfigOnChange(globalConfig, globalConfig.RawConfigItems);
            Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            UiUser[]? users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn });
            if (users.Length > 0)
            {
                User = users[0];
            }
            await InitWithUserId(apiConnection, User.DbId);

            if (User.Language == null)
            {
                User.Language = DefaultLanguage;
            }
            await ChangeLanguage(User.Language, apiConnection);
        }

        public async Task ChangeLanguage(string languageName, ApiConnection apiConnection)
        {
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserLanguage, new { id = User.DbId, language = languageName });
            Translate = globalConfig.LangDict[languageName];
            Overwrite = apiConnection != null ? await GetCustomDict(languageName): globalConfig.OverDict[languageName];
            User.Language = languageName;
            InvokeOnChange(this, null);
        }

        public string GetUserLanguage()
        {
            return User.Language ?? "";
        }

        public void SetLanguage(string languageName)
        {
            User = new UiUser() { Language = globalConfig.DefaultLanguage };
            if (languageName != null && languageName != "")
            {
                User.Language = languageName;
            }
            if (globalConfig.LangDict.TryGetValue(User.Language, out Dictionary<string, string>? langDict))
            {
                Translate = langDict;
                Overwrite = globalConfig.OverDict[User.Language];
            }
        }

        public override string GetText(string key)
        {
            if (Overwrite != null && Overwrite.TryGetValue(key, out string? overwriteValue))
            {
                return Convert(overwriteValue);
            }
            if (Translate != null && Translate.TryGetValue(key, out string? translateValue))
            {
                return Convert(translateValue);
            }
            else
            {
                string defaultLanguage = globalConfig.DefaultLanguage;
                if (defaultLanguage == "")
                {
                    defaultLanguage = GlobalConst.kEnglish;
                }
                if (globalConfig.LangDict[defaultLanguage].TryGetValue(key, out string? defaultLangValue))
                {
                    return Convert(defaultLangValue);
                }
                else if (defaultLanguage != GlobalConst.kEnglish && globalConfig.LangDict[GlobalConst.kEnglish].TryGetValue(key, out string? englValue))
                {
                    return Convert(englValue);
                }
                else
                {
                    return GlobalConst.kUndefinedText;
                }
            }
        }

        public string PureLine(string text)
        {
            string output = RemoveLinks(Regex.Replace(GetText(text).Trim(), @"\s", " "));
            output = ReplaceListElems(output);
            bool cont = true;
            while(cont)
            {
                string outputOrig = output;
                output = Regex.Replace(outputOrig, @"  ", " ");
                if(output.Length == outputOrig.Length)
                {
                    cont = false;
                }
            }
            return output;
        }

        public string GetApiText(string key)
        {
            string text = key;
            string pattern = @"[A]\d\d\d\d";
            Match m = Regex.Match(key, pattern);
            if (m.Success)
            {
                string msg = GetText(key.Substring(0, 5));
                if (msg != GlobalConst.kUndefinedText)
                {
                    text = msg;
                }
            }
            return text;
        }

        public async Task<Dictionary<string, string>> GetCustomDict(string languageName)
        {
            Dictionary<string, string> dict = [];
            try
            {
                List<UiText> uiTexts = await apiConnection.SendQueryAsync<List<UiText>>(ConfigQueries.getCustomTextsPerLanguage, new { language = languageName });
                if (uiTexts != null)
                {
                    foreach (UiText text in uiTexts)
                    {
                        dict.Add(text.Id, text.Txt);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Read custom dictionary", $"Could not read custom dict.", exception);
            }
            return dict;
        }

        private static string RemoveLinks(string txtString)
        {
            string startLink = "<a href=\"/";
            int begin, end;
            int index = 0;
            bool cont = true;

            while (cont)
            {
                begin = txtString.IndexOf(startLink, index);
                if (begin >= 0)
                {
                    end = txtString.IndexOf('>', begin + startLink.Length);
                    if (end > 0)
                    {
                        txtString = txtString.Remove(begin, end - begin + 1);
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
            txtString = Regex.Replace(txtString, "</a>", "");
            return txtString;
        }
    
        private static string ReplaceListElems(string txtString)
        {
            txtString = Regex.Replace(txtString, "<ol>", "");
            txtString = Regex.Replace(txtString, "</ol>", "");
            txtString = Regex.Replace(txtString, "<ul>", "");
            txtString = Regex.Replace(txtString, "</ul>", "");
            txtString = Regex.Replace(txtString, "<li>", "\r\n");
            txtString = Regex.Replace(txtString, "</li>", "");
            txtString = Regex.Replace(txtString, "<br>", "\r\n");
            return txtString;
        }
        
        private string Convert(string rawText)
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
                    if (begin >= 0)
                    {
                        end = plainText.IndexOf('"', begin + startLink.Length);
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
    }
}
