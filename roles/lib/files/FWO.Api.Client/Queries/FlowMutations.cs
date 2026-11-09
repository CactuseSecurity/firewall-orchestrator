using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class FlowMutations : Queries
    {
        public static readonly string insertFlowNwObject;
        public static readonly string resetFlowDB;
        public static readonly string upsertFlowNwObjectMapping;
        public static readonly string upsertFlowNwGroupMapping;
        public static readonly string upsertFlowSvcObjectMapping;
        public static readonly string upsertFlowSvcGroupMapping;
        public static readonly string upsertFlowTimeObjectMapping;
        public static readonly string updateFlowNwGroup;
        public static readonly string updateFlowNwObject;
        public static readonly string updateFlowNwGroups;
        public static readonly string updateFlowNwObjects;
        public static readonly string updateFlowNwObjectVisibility;
        public static readonly string updateFlowSvcGroup;
        public static readonly string updateFlowSvcObject;
        public static readonly string updateFlowSvcGroups;
        public static readonly string updateFlowSvcObjects;
        public static readonly string updateFlowTimeObject;
        public static readonly string updateFlowTimeObjects;

        static FlowMutations()
        {
            try
            {
                insertFlowNwObject = GetQueryText("flow/mutations/insertFlowNwObject.graphql");
                resetFlowDB = GetQueryText("flow/mutations/resetFlowDB.graphql");
                upsertFlowNwObjectMapping = GetQueryText("flow/mutations/upsertFlowNwObjectMapping.graphql");
                upsertFlowNwGroupMapping = GetQueryText("flow/mutations/upsertFlowNwGroupMapping.graphql");
                upsertFlowSvcObjectMapping = GetQueryText("flow/mutations/upsertFlowSvcObjectMapping.graphql");
                upsertFlowSvcGroupMapping = GetQueryText("flow/mutations/upsertFlowSvcGroupMapping.graphql");
                upsertFlowTimeObjectMapping = GetQueryText("flow/mutations/upsertFlowTimeObjectMapping.graphql");
                updateFlowNwGroup = GetQueryText("flow/mutations/updateFlowNwGroup.graphql");
                updateFlowNwObject = GetQueryText("flow/mutations/updateFlowNwObject.graphql");
                updateFlowNwGroups = GetQueryText("flow/mutations/updateFlowNwGroups.graphql");
                updateFlowNwObjects = GetQueryText("flow/mutations/updateFlowNwObjects.graphql");
                updateFlowNwObjectVisibility = GetQueryText("flow/mutations/updateFlowNwObjectVisibility.graphql");
                updateFlowSvcGroup = GetQueryText("flow/mutations/updateFlowSvcGroup.graphql");
                updateFlowSvcObject = GetQueryText("flow/mutations/updateFlowSvcObject.graphql");
                updateFlowSvcGroups = GetQueryText("flow/mutations/updateFlowSvcGroups.graphql");
                updateFlowSvcObjects = GetQueryText("flow/mutations/updateFlowSvcObjects.graphql");
                updateFlowTimeObject = GetQueryText("flow/mutations/updateFlowTimeObject.graphql");
                updateFlowTimeObjects = GetQueryText("flow/mutations/updateFlowTimeObjects.graphql");
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
