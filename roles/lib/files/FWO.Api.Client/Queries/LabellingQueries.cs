using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class LabellingQueries : Queries
    {
        public static readonly string getLabelKeys;
        public static readonly string getLabelIds;
        public static readonly string upsertImportedLabels;
        public static readonly string addLabelChangeEvent;
        public static readonly string getOwnerNetworkLabels;
        public static readonly string addOwnerNetworkLabels;
        public static readonly string removeOwnerNetworkLabels;
        public static readonly string upsertAppLabelKey;
        public static readonly string upsertLabelValues;
        public static readonly string getActiveRuleOwnerApps;
        public static readonly string getAllActiveRuleOwnerApps;
        public static readonly string getActiveRuleAppLabels;
        public static readonly string getAllActiveRuleAppLabels;
        public static readonly string addRuleLabelAssignments;
        public static readonly string removeRuleLabelAssignments;
        public static readonly string getOwnerAppServerAppLabels;

        static LabellingQueries()
        {
            try
            {
                getLabelKeys = GetQueryText("labelling/getLabelKeys.graphql");
                getLabelIds = GetQueryText("labelling/getLabelIds.graphql");
                upsertImportedLabels = GetQueryText("labelling/upsertImportedLabels.graphql");
                addLabelChangeEvent = GetQueryText("labelling/addLabelChangeEvent.graphql");
                getOwnerNetworkLabels = GetQueryText("labelling/getOwnerNetworkLabels.graphql");
                addOwnerNetworkLabels = GetQueryText("labelling/addOwnerNetworkLabels.graphql");
                removeOwnerNetworkLabels = GetQueryText("labelling/removeOwnerNetworkLabels.graphql");
                upsertAppLabelKey = GetQueryText("labelling/upsertAppLabelKey.graphql");
                upsertLabelValues = GetQueryText("labelling/upsertLabelValues.graphql");
                getActiveRuleOwnerApps = GetQueryText("labelling/getActiveRuleOwnerApps.graphql");
                getAllActiveRuleOwnerApps = GetQueryText("labelling/getAllActiveRuleOwnerApps.graphql");
                getActiveRuleAppLabels = GetQueryText("labelling/getActiveRuleAppLabels.graphql");
                getAllActiveRuleAppLabels = GetQueryText("labelling/getAllActiveRuleAppLabels.graphql");
                addRuleLabelAssignments = GetQueryText("labelling/addRuleLabelAssignments.graphql");
                removeRuleLabelAssignments = GetQueryText("labelling/removeRuleLabelAssignments.graphql");
                getOwnerAppServerAppLabels = GetQueryText("labelling/getOwnerAppServerAppLabels.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize LabellingQueries", "Api LabellingQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
