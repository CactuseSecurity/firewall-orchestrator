using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class ConfigQueries : Queries
    {
        public static readonly string getLanguages;
        public static readonly string getAllTexts;
        public static readonly string getTextsPerLanguage;
        public static readonly string addConfigItem;
        public static readonly string updateConfigItem;
        public static readonly string upsertConfigItem;
        public static readonly string getConfigItemsByUser;
        public static readonly string subscribeAutodiscoveryConfigChanges;
        public static readonly string subscribeDailyCheckConfigChanges;

        static ConfigQueries()
        {
            try
            {
                getLanguages = File.ReadAllText(QueryPath + "config/getLanguages.graphql");
                getAllTexts = File.ReadAllText(QueryPath + "config/getTexts.graphql");
                getTextsPerLanguage = File.ReadAllText(QueryPath + "config/getTextsPerLanguage.graphql");
                addConfigItem = File.ReadAllText(QueryPath + "config/addConfigItem.graphql");
                updateConfigItem = File.ReadAllText(QueryPath + "config/updateConfigItem.graphql");
                getConfigItemsByUser = File.ReadAllText(QueryPath + "config/getConfigItemsByUser.graphql");
                upsertConfigItem = File.ReadAllText(QueryPath + "config/upsertConfigItem.graphql");
                subscribeAutodiscoveryConfigChanges = File.ReadAllText(QueryPath + "config/subscribeAutodiscoveryConfigChanges.graphql");
                subscribeDailyCheckConfigChanges = File.ReadAllText(QueryPath + "config/subscribeDailyCheckConfigChanges.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ConfigQueries", "Api ConfigQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
