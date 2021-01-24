using System.Collections.Generic;
using FWO.Logging;
using FWO.ApiConfig.Data;
using System.Linq;
using System;
using FWO.ApiClient;
using FWO.Api.Data;
using FWO.ApiClient.Queries;
using System.Threading.Tasks;

namespace FWO.ApiConfig
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfig
    {
        private readonly GlobalConfig globalConfig;

        private Dictionary<string, string> userConfigItems;
        private Dictionary<string, string> defaultConfigItems;

        public Dictionary<string, string> Translate {get; set; }

        public UiUser User { private set; get; }

        public event Func<UserConfig, Task> OnChange;

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users
        /// </summary>
        /// 
        public UserConfig(GlobalConfig globalConfigIn)
        {
            Translate = globalConfigIn.langDict[globalConfigIn.defaultLanguage];
            globalConfig = globalConfigIn;
        }

        public async Task SetUserInformation(string userDn, APIConnection apiConnection)
        {
            Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            User = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn }))?[0];

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
                result.Add(confItem.Key, confItem.Value);
            }

            return result;
        }

        public string GetUserLanguage()
        {
            return User.Language;
        }

        public async Task ChangeLanguage(string languageName, APIConnection apiConnection)
        {
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserLanguage, new { id = User.DbId, language = languageName });

            Translate = globalConfig.langDict[languageName];
            if (OnChange != null)
                await OnChange.Invoke(this);
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
                    await Task.Run(() => apiConnection.SendQueryAsync<object>(ConfigQueries.addConfigItem, apiVariables));
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
