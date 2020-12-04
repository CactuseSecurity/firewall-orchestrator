using System.Collections.Generic;
using FWO.Logging;
using FWO.ApiConfig.Data;
using System.Linq;
using System;

namespace FWO.ApiConfig
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfigCollection
    {
        public string CurrentLanguage { get; private set; }
        protected ConfigCollection globalConfig { get; set; }
 
        public Dictionary<string, string> userConfigItems { get; set; }
        public Dictionary<string, string> defaultConfigItems { get; set; }

        public Dictionary<string, string> translate;

        public Dictionary<string, string> Translate
        {
            get
            {
                return translate;
            }

            set
            {
                translate = value;
            }
        }

        public event EventHandler OnChange;

        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users
        /// </summary>
        public UserConfigCollection(ConfigCollection globalConfigIn)
        {
            CurrentLanguage = globalConfigIn.defaultLanguage;   // TODO: not quite correct when a user signs back in and has alread set another language; Do not create a new UserConfigCollection then?
            translate = globalConfigIn.langDict[CurrentLanguage];
            globalConfig = globalConfigIn;
        }

        public void ChangeLanguage(string languageName)
        {
            CurrentLanguage = languageName;
            translate = globalConfig.langDict[languageName];
            if (OnChange != null)
                OnChange.Invoke(this, null);
        }

        public string GetConfigValue(string Key)
        {
            string settingsValue = "";
            if (userConfigItems.ContainsKey(Key))
            {
                settingsValue = userConfigItems[Key];
            }
            else if (defaultConfigItems.ContainsKey(Key))
            {
                settingsValue = defaultConfigItems[Key];
            }
            return settingsValue;
        }

        public void ChangeConfigValue(string Key, string Value)
        {
            if (userConfigItems.ContainsKey(Key))
            {
                userConfigItems[Key] = Value;
            }
            else
            {
                userConfigItems.Add(Key, Value);
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
                    translate = globalConfig.langDict[CurrentLanguage];
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
    }
}
