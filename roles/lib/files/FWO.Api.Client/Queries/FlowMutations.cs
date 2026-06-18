using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class FlowMutations : Queries
    {
        public static readonly string insertFlowNwObject;
        public static readonly string resetFlowDB;
        public static readonly string upsertFlowNwObjectMapping;
        public static readonly string updateFlowNwGroup;
        public static readonly string updateFlowNwObject;
        public static readonly string updateFlowNwObjectVisibility;
        public static readonly string updateFlowSvcGroup;
        public static readonly string updateFlowSvcObject;
        public static readonly string updateFlowTimeObject;

        static FlowMutations()
        {
            try
            {
                insertFlowNwObject = GetQueryText("flow/mutations/insertFlowNwObject.graphql");
                resetFlowDB = GetQueryText("flow/mutations/resetFlowDB.graphql");
                upsertFlowNwObjectMapping = GetQueryText("flow/mutations/upsertFlowNwObjectMapping.graphql");
                updateFlowNwGroup = GetQueryText("flow/mutations/updateFlowNwGroup.graphql");
                updateFlowNwObject = GetQueryText("flow/mutations/updateFlowNwObject.graphql");
                updateFlowNwObjectVisibility = GetQueryText("flow/mutations/updateFlowNwObjectVisibility.graphql");
                updateFlowSvcGroup = GetQueryText("flow/mutations/updateFlowSvcGroup.graphql");
                updateFlowSvcObject = GetQueryText("flow/mutations/updateFlowSvcObject.graphql");
                updateFlowTimeObject = GetQueryText("flow/mutations/updateFlowTimeObject.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Flow Mutations could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
