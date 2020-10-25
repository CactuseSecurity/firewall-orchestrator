using System.Collections.Generic;
using FWO.Logging;
using FWO.ApiConfig.Data;

namespace FWO.ApiConfig
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfigCollection
    {
        public string CurrentLanguage { get; set; }
        protected ConfigCollection globalConfig { get; set; }

        public Dictionary<string, string> translate;
        /// <summary>
        /// create a config collection (used centrally once in a UI server for all users
        /// </summary>

        public UserConfigCollection(ConfigCollection globalConfigIn)
        {
            CurrentLanguage = globalConfigIn.defaultLanguage;   // TODO: not quite correct when a user signs back in and has alread set another language; Do not create a new UserConfigCollection then?
            translate = globalConfigIn.langDict[CurrentLanguage];
            globalConfig = globalConfigIn;
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


            if (!changedLanguage)
            {
                Log.WriteWarning("Language Config", "Something went wrong while trying to switch languages.");
            }
        }
    }
}
