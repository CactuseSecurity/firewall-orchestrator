using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ProvisioningQueries : Queries
    {
        public static readonly string getNodeWithAncestors;
        public static readonly string getNodeById;
        public static readonly string getNodesByNaturalKeys;
        public static readonly string getAllNodes;
        public static readonly string upsertNode;
        public static readonly string applyPatch;
        public static readonly string deleteOverrides;

        static ProvisioningQueries()
        {
            try
            {
                getNodeWithAncestors = GetQueryText("provisioning/getNodeWithAncestors.graphql");
                getNodeById = GetQueryText("provisioning/getNodeById.graphql");
                getNodesByNaturalKeys = GetQueryText("provisioning/getNodesByNaturalKeys.graphql");
                getAllNodes = GetQueryText("provisioning/getAllNodes.graphql");
                upsertNode = GetQueryText("provisioning/upsertNode.graphql");
                applyPatch = GetQueryText("provisioning/applyPatch.graphql");
                deleteOverrides = GetQueryText("provisioning/deleteOverrides.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ProvisioningQueries", "Provisioning queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
