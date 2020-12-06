using System.Collections.Generic;
using FWO.Logging;
using FWO.ApiConfig.Data;
using System.Linq;
using System;
using FWO.ApiClient;

namespace FWO.ApiConfig
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfig
    {
        public string CurrentLanguage { get; private set; }
        protected GlobalConfig globalConfig { get; set; }
 
        public Dictionary<string, string> UserConfigItems { get; set; }
        public Dictionary<string, string> DefaultConfigItems { get; set; }

        public Dictionary<string, string> Translate {get; set; }

        public event EventHandler OnChange;

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users
        /// </summary>
        /// 
        //public UserConfig()
        //{
        //    ClaimsPrincipal user = authState.User;
        //    string userDn = user.FindFirstValue("x-hasura-uuid");

        //    //UiUser = apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn }).Result?[0]; // SHORTER

        //    Log.WriteDebug("Get User Data", $"userDn: {userDn}");
        //    UiUser[] uiUsers = (Task.Run(() => apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUserByDn, new { dn = userDn }))).Result;
        //    if (uiUsers != null && uiUsers.Length > 0)
        //    {
        //        UiUser = uiUsers[0];
        //    }
        //}

        public UserConfig(GlobalConfig globalConfigIn)//, string userDn, APIConnection apiConnection)
        {
            CurrentLanguage = globalConfigIn.defaultLanguage;   // TODO: not quite correct when a user signs back in and has alread set another language; Do not create a new UserConfigCollection then?
            Translate = globalConfigIn.langDict[CurrentLanguage];
            globalConfig = globalConfigIn;

            //UiUser = apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn }).Result?[0]; // SHORTER

            //Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            //UiUser = apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUserByDn, new { dn = userDn }).Result[0];
            //if (uiUsers != null && uiUsers.Length > 0)
            //{
            //    UiUser = uiUsers[0];
            //}
        }

        public void ChangeLanguage(string languageName)
        {
            CurrentLanguage = languageName;
            Translate = globalConfig.langDict[languageName];
            if (OnChange != null)
                OnChange.Invoke(this, null);
        }

        public string GetConfigValue(string Key)
        {
            string settingsValue = "";
            if (UserConfigItems.ContainsKey(Key))
            {
                settingsValue = UserConfigItems[Key];
            }
            else if (DefaultConfigItems.ContainsKey(Key))
            {
                settingsValue = DefaultConfigItems[Key];
            }
            return settingsValue;
        }

        public void ChangeConfigValue(string Key, string Value)
        {
            if (UserConfigItems.ContainsKey(Key))
            {
                UserConfigItems[Key] = Value;
            }
            else
            {
                UserConfigItems.Add(Key, Value);
            }
        }

        public void setNextLanguage()
        {
            int idx = 0;
            bool changedLanguage = false;

            foreach (Language lang in globalConfig.uiLanguages)
            {
                if (lang.Name == CurrentLanguage)
                {
                    CurrentLanguage = globalConfig.uiLanguages[(idx + 1) % (globalConfig.uiLanguages.Length)].Name;
                    Translate = globalConfig.langDict[CurrentLanguage];
                    changedLanguage = true;
                    //ComponentBase.StateHasChanged();
                    break;
                }
                idx++;
            }

            OnChange.Invoke(this, null);

            if (!changedLanguage)
            {
                Log.WriteWarning("Language Config", "Something went wrong while trying to switch languages.");
            }
        }

        //public UiUser UiUser { get; set; }

        //public async Task ChangeLanguage(string language, APIConnection apiConnection)
        //{
        //    try
        //    {
        //        var Variables = new
        //        {
        //            id = UiUser.DbId,
        //            language = language
        //        };
        //        await Task.Run(() => apiConnection.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUser, Variables));
        //    }
        //    catch (Exception)
        //    {
        //        // maybe admin has deleted uiuser inbetween
        //    }
        //}
    }
}
