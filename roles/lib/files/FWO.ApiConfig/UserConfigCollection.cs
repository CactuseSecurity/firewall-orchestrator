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
            CurrentLanguage = "English";
            translate = globalConfigIn.langDict[CurrentLanguage];
            globalConfig = globalConfigIn;
        }
        public void setNextLanguage()
        {
            int idx = 0;
            bool changedLanguage = false;

            //int currentIdx = globalConfig.uiLanguages.FindIndex(globalConfig.uiLanguages, l => l.IsKey);
            foreach (Language lang in globalConfig.uiLanguages)
            {
                if (lang.Name == CurrentLanguage)
                {
                    CurrentLanguage = globalConfig.uiLanguages[(idx + 1) % (globalConfig.uiLanguages.Length)].Name;
                    translate = globalConfig.langDict[CurrentLanguage];
                    changedLanguage = true;
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





// using FWO.Logging;
// using FWO.ApiConfig.Data;

// namespace FWO.ApiConfig
// {
//     /// <summary>
//     /// Collection of all config data for the current user
//     /// </summary>
//     public class UserConfigCollection : ConfigCollection
//     {
//         public string CurrentLanguage; // = defaultLanguage;
//         public string jwt;

//         /// <summary>
//         /// create a config collection (used centrally once in a UI server for all users
//         /// </summary>
//         public UserConfigCollection(string jwt) : base(jwt)
//         {

//         }

//         public void setNextLanguage()
//         {
//             int idx = 0;
//             bool changedLanguage = false;
//             foreach (Language lang in uiLanguages)
//             {
//                 if (lang.Name == CurrentLanguage)
//                 {
//                     CurrentLanguage = uiLanguages[(idx + 1) % uiLanguages.Length].Name;
//                      changedLanguage = true;
//                 }
//                 idx++;
//             }
//             if (!changedLanguage)
//             {
//                 Log.WriteWarning("Language Config","Something went wrong while trying to switch languages.");
//             }
//         }
//     }
// }
