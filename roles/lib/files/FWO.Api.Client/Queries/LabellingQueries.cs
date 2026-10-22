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
