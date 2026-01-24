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
        public static readonly string subscribeImportIpDataConfigChanges;
        public static readonly string subscribeImportNotifyConfigChanges;
        public static readonly string subscribeVarianceAnalysisConfigChanges;
        public static readonly string subscribeComplianceCheckConfigChanges;


        static ConfigQueries()
        {
            try
            {
                getLanguages = GetQueryText("config/getLanguages.graphql");
                getAllTexts = GetQueryText("config/getTexts.graphql");
                getTextsPerLanguage = GetQueryText("config/getTextsPerLanguage.graphql");
                getCustomTextsPerLanguage = GetQueryText("config/getCustomTextsPerLanguage.graphql");
                upsertCustomText = GetQueryText("config/upsertCustomText.graphql");
                deleteCustomText = GetQueryText("config/deleteCustomText.graphql");
                subscribeConfigChangesByUser = GetQueryText("config/subscribeConfigChangesByUser.graphql");
                addConfigItem = GetQueryText("config/addConfigItem.graphql");
                updateConfigItem = GetQueryText("config/updateConfigItem.graphql");
                getConfigItemsByUser = GetQueryText("config/getConfigItemsByUser.graphql");
                getConfigItemByKey = GetQueryText("config/getConfigItemByKey.graphql");
                upsertConfigItem = GetQueryText("config/upsertConfigItem.graphql");
                upsertConfigItems = GetQueryText("config/upsertConfigItems.graphql");
                subscribeAutodiscoveryConfigChanges = GetQueryText("config/subscribeAutodiscoveryConfigChanges.graphql");
                subscribeExternalRequestConfigChanges = GetQueryText("config/subscribeExternalRequestConfigChanges.graphql");
                subscribeDailyCheckConfigChanges = GetQueryText("config/subscribeDailyCheckConfigChanges.graphql");
                subscribeImportAppDataConfigChanges = GetQueryText("config/subscribeImportAppDataConfigChanges.graphql");
                subscribeImportIpDataConfigChanges = GetQueryText("config/subscribeImportSubnetDataConfigChanges.graphql");
                subscribeImportNotifyConfigChanges = GetQueryText("config/subscribeImportNotifyConfigChanges.graphql");
                subscribeVarianceAnalysisConfigChanges = GetQueryText("config/subscribeVarianceAnalysisConfigChanges.graphql");
                subscribeComplianceCheckConfigChanges = GetQueryText("config/subscribeComplianceCheckConfigChanges.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ConfigQueries", "Api ConfigQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
