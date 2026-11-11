using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class DeviceSpecificSettingsQueries : Queries
    {
        public static readonly string getNodeWithAncestors;
        public static readonly string getNodeById;
        public static readonly string getNodesByNaturalKeys;
        public static readonly string getAllNodes;
        public static readonly string upsertNode;
        public static readonly string applyPatch;
        public static readonly string deleteOverrides;

        static DeviceSpecificSettingsQueries()
        {
            try
            {
                getNodeWithAncestors = GetQueryText("deviceSpecificSettings/getNodeWithAncestors.graphql");
                getNodeById = GetQueryText("deviceSpecificSettings/getNodeById.graphql");
                getNodesByNaturalKeys = GetQueryText("deviceSpecificSettings/getNodesByNaturalKeys.graphql");
                getAllNodes = GetQueryText("deviceSpecificSettings/getAllNodes.graphql");
                upsertNode = GetQueryText("deviceSpecificSettings/upsertNode.graphql");
                applyPatch = GetQueryText("deviceSpecificSettings/applyPatch.graphql");
                deleteOverrides = GetQueryText("deviceSpecificSettings/deleteOverrides.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceSpecificSettingsQueries", "Device specific settings queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
