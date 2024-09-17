using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ConfigQueries : Queries
    {
        public static readonly string getLanguages;
        public static readonly string getAllTexts;
        public static readonly string getTextsPerLanguage;
        public static readonly string getCustomTextsPerLanguage;
        public static readonly string upsertCustomText;
        public static readonly string deleteCustomText;
        public static readonly string subscribeConfigChangesByUser;
        public static readonly string addConfigItem;
        public static readonly string updateConfigItem;
        public static readonly string upsertConfigItem;
        public static readonly string upsertConfigItems;
        public static readonly string getConfigItemsByUser;
        public static readonly string getConfigItemByKey;
        public static readonly string subscribeAutodiscoveryConfigChanges;
        public static readonly string subscribeExternalRequestConfigChanges;
        public static readonly string subscribeDailyCheckConfigChanges;
        public static readonly string subscribeImportAppDataConfigChanges;
        public static readonly string subscribeImportSubnetDataConfigChanges;
        public static readonly string subscribeImportNotifyConfigChanges;


        static ConfigQueries()
        {
            try
            {
                getLanguages = File.ReadAllText(QueryPath + "config/getLanguages.graphql");
                getAllTexts = File.ReadAllText(QueryPath + "config/getTexts.graphql");
                getTextsPerLanguage = File.ReadAllText(QueryPath + "config/getTextsPerLanguage.graphql");
                getCustomTextsPerLanguage = File.ReadAllText(QueryPath + "config/getCustomTextsPerLanguage.graphql");
                upsertCustomText = File.ReadAllText(QueryPath + "config/upsertCustomText.graphql");
                deleteCustomText = File.ReadAllText(QueryPath + "config/deleteCustomText.graphql");
                subscribeConfigChangesByUser = File.ReadAllText(QueryPath + "config/subscribeConfigChangesByUser.graphql");
                addConfigItem = File.ReadAllText(QueryPath + "config/addConfigItem.graphql");
                updateConfigItem = File.ReadAllText(QueryPath + "config/updateConfigItem.graphql");
                getConfigItemsByUser = File.ReadAllText(QueryPath + "config/getConfigItemsByUser.graphql");
                getConfigItemByKey = File.ReadAllText(QueryPath + "config/getConfigItemByKey.graphql");
                upsertConfigItem = File.ReadAllText(QueryPath + "config/upsertConfigItem.graphql");
                upsertConfigItems = File.ReadAllText(QueryPath + "config/upsertConfigItems.graphql");
                subscribeAutodiscoveryConfigChanges = File.ReadAllText(QueryPath + "config/subscribeAutodiscoveryConfigChanges.graphql");
                subscribeExternalRequestConfigChanges = File.ReadAllText(QueryPath + "config/subscribeExternalRequestConfigChanges.graphql");
                subscribeDailyCheckConfigChanges = File.ReadAllText(QueryPath + "config/subscribeDailyCheckConfigChanges.graphql");
                subscribeImportAppDataConfigChanges = File.ReadAllText(QueryPath + "config/subscribeImportAppDataConfigChanges.graphql");
                subscribeImportSubnetDataConfigChanges = File.ReadAllText(QueryPath + "config/subscribeImportSubnetDataConfigChanges.graphql");
                subscribeImportNotifyConfigChanges = File.ReadAllText(QueryPath + "config/subscribeImportNotifyConfigChanges.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ConfigQueries", "Api ConfigQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
