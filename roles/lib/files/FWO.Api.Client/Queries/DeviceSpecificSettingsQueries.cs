using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class DeviceSpecificSettingsQueries : Queries
    {
        public static readonly string getNodeWithAncestors;
        public static readonly string getNodeById;
        public static readonly string getNodesByNaturalKeys;
        public static readonly string getAllNodes;
        public static readonly string getNodesUsingCredential;
        public static readonly string upsertNode;
        public static readonly string applyPatch;
        public static readonly string deleteOverrides;
        public static readonly string getAllNodesWithValues;
        public static readonly string applyBulkPatch;
        public static readonly string getSnapshots;
        public static readonly string getSnapshotById;
        public static readonly string addSnapshot;
        public static readonly string deleteSnapshot;

        static DeviceSpecificSettingsQueries()
        {
            try
            {
                getNodeWithAncestors = GetQueryText("deviceSpecificSettings/getNodeWithAncestors.graphql");
                getNodeById = GetQueryText("deviceSpecificSettings/getNodeById.graphql");
                getNodesByNaturalKeys = GetQueryText("deviceSpecificSettings/getNodesByNaturalKeys.graphql");
                getAllNodes = GetQueryText("deviceSpecificSettings/getAllNodes.graphql");
                getNodesUsingCredential = GetQueryText("deviceSpecificSettings/getNodesUsingCredential.graphql");
                upsertNode = GetQueryText("deviceSpecificSettings/upsertNode.graphql");
                applyPatch = GetQueryText("deviceSpecificSettings/applyPatch.graphql");
                deleteOverrides = GetQueryText("deviceSpecificSettings/deleteOverrides.graphql");
                getAllNodesWithValues = GetQueryText("deviceSpecificSettings/getAllNodesWithValues.graphql");
                applyBulkPatch = GetQueryText("deviceSpecificSettings/applyBulkPatch.graphql");
                getSnapshots = GetQueryText("deviceSpecificSettings/getSnapshots.graphql");
                getSnapshotById = GetQueryText("deviceSpecificSettings/getSnapshotById.graphql");
                addSnapshot = GetQueryText("deviceSpecificSettings/addSnapshot.graphql");
                deleteSnapshot = GetQueryText("deviceSpecificSettings/deleteSnapshot.graphql");
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
