using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class FlowQueries : Queries
    {
        public static readonly string flowAccessDetailsFragment;
        public static readonly string flowNwGroupDetailsFragment;
        public static readonly string flowNwObjectDetailsFragment;
        public static readonly string flowSvcGroupDetailsFragment;
        public static readonly string flowSvcObjectDetailsFragment;
        public static readonly string flowTimeObjectDetailsFragment;
        public static readonly string getFlowAccessCatalog;
        public static readonly string getFlowCustomObjectCandidates;
        public static readonly string getFlowNwObjectCatalog;
        public static readonly string getFlowSvcObjectCatalog;
        public static readonly string getFlowObjectCatalog;
        public static readonly string getFlowSelectableManagements;
        // Flow sync specific queries/mutations
        public static readonly string getFlowSyncData;
        public static readonly string getFlowSyncNwObjects;
        public static readonly string getFlowSyncNwGroups;
        public static readonly string getFlowSyncSvcObjects;
        public static readonly string getFlowSyncSvcGroups;
        public static readonly string getFlowSyncTimeObjects;
        public static readonly string getFlowSyncAccesses;
        public static readonly string getPendingFlowSyncImports;
        public static readonly string networkObjectFlowSyncDetails;
        public static readonly string networkServiceFlowSyncDetails;
        public static readonly string timeObjectFlowSyncDetails;
        public static readonly string ruleFlowSyncDetails;
        public static readonly string getFlowSyncManagementData;
        public static readonly string insertFlowNwObjects;
        public static readonly string insertFlowSvcObjects;
        public static readonly string insertFlowTimeObjects;
        public static readonly string insertFlowNwGroups;
        public static readonly string insertFlowNwGroupMembers;
        public static readonly string insertFlowSvcGroups;
        public static readonly string insertFlowSvcGroupMembers;
        public static readonly string insertFlowAccesses;
        public static readonly string updateObjectFlowMappings;
        public static readonly string updateServiceFlowMappings;
        public static readonly string updateTimeObjectFlowMappings;
        public static readonly string updateRuleFlowMappings;
        public static readonly string updateImportControlForFlowSync;
        public static readonly string updateFlowMappingsForRemoved;

        static FlowQueries()
        {
            try
            {
                flowAccessDetailsFragment = GetQueryText("flow/fragments/flowAccessDetails.graphql");
                flowNwGroupDetailsFragment = GetQueryText("flow/fragments/flowNwGroupDetails.graphql");
                flowNwObjectDetailsFragment = GetQueryText("flow/fragments/flowNwObjectDetails.graphql");
                flowSvcGroupDetailsFragment = GetQueryText("flow/fragments/flowSvcGroupDetails.graphql");
                flowSvcObjectDetailsFragment = GetQueryText("flow/fragments/flowSvcObjectDetails.graphql");
                flowTimeObjectDetailsFragment = GetQueryText("flow/fragments/flowTimeObjectDetails.graphql");

                getFlowAccessCatalog =
                    flowAccessDetailsFragment +
                    GetQueryText("flow/getFlowAccessCatalog.graphql");

                getFlowCustomObjectCandidates =
                    GetQueryText("flow/getFlowCustomObjectCandidates.graphql");

                getFlowNwObjectCatalog =
                    flowNwObjectDetailsFragment +
                    GetQueryText("flow/getFlowNwObjectCatalog.graphql");

                getFlowSvcObjectCatalog =
                    flowSvcObjectDetailsFragment +
                    GetQueryText("flow/getFlowSvcObjectCatalog.graphql");

                getFlowObjectCatalog =
                    flowNwObjectDetailsFragment +
                    flowNwGroupDetailsFragment +
                    flowSvcObjectDetailsFragment +
                    flowSvcGroupDetailsFragment +
                    flowTimeObjectDetailsFragment +
                    GetQueryText("flow/getFlowObjectCatalog.graphql");

                getFlowSelectableManagements =
                    GetQueryText("flow/getFlowSelectableManagements.graphql");

                // Flow sync specific files
                getFlowSyncData = GetQueryText("flowSync/getFlowSyncData.graphql");
                getFlowSyncNwObjects = GetQueryText("flowSync/getFlowSyncNwObjects.graphql");
                getFlowSyncNwGroups = GetQueryText("flowSync/getFlowSyncNwGroups.graphql");
                getFlowSyncSvcObjects = GetQueryText("flowSync/getFlowSyncSvcObjects.graphql");
                getFlowSyncSvcGroups = GetQueryText("flowSync/getFlowSyncSvcGroups.graphql");
                getFlowSyncTimeObjects = GetQueryText("flowSync/getFlowSyncTimeObjects.graphql");
                getFlowSyncAccesses = GetQueryText("flowSync/getFlowSyncAccesses.graphql");
                getPendingFlowSyncImports = GetQueryText("flowSync/getPendingFlowSyncImports.graphql");

                networkObjectFlowSyncDetails = GetQueryText("flowSync/fragments/networkObjectFlowSyncDetails.graphql");
                networkServiceFlowSyncDetails = GetQueryText("flowSync/fragments/networkServiceFlowSyncDetails.graphql");
                timeObjectFlowSyncDetails = GetQueryText("flowSync/fragments/timeObjectFlowSyncDetails.graphql");
                ruleFlowSyncDetails = GetQueryText("flowSync/fragments/ruleFlowSyncDetails.graphql");

                getFlowSyncManagementData =
                    networkObjectFlowSyncDetails +
                    networkServiceFlowSyncDetails +
                    timeObjectFlowSyncDetails +
                    ruleFlowSyncDetails +
                    GetQueryText("flowSync/getFlowSyncManagementData.graphql");

                insertFlowNwObjects = GetQueryText("flowSync/insertFlowNwObjects.graphql");
                insertFlowSvcObjects = GetQueryText("flowSync/insertFlowSvcObjects.graphql");
                insertFlowTimeObjects = GetQueryText("flowSync/insertFlowTimeObjects.graphql");
                insertFlowNwGroups = GetQueryText("flowSync/insertFlowNwGroups.graphql");
                insertFlowNwGroupMembers = GetQueryText("flowSync/insertFlowNwGroupMembers.graphql");
                insertFlowSvcGroups = GetQueryText("flowSync/insertFlowSvcGroups.graphql");
                insertFlowSvcGroupMembers = GetQueryText("flowSync/insertFlowSvcGroupMembers.graphql");
                insertFlowAccesses = GetQueryText("flowSync/insertFlowAccesses.graphql");

                updateObjectFlowMappings = GetQueryText("flowSync/updateObjectFlowMappings.graphql");
                updateServiceFlowMappings = GetQueryText("flowSync/updateServiceFlowMappings.graphql");
                updateTimeObjectFlowMappings = GetQueryText("flowSync/updateTimeObjectFlowMappings.graphql");
                updateRuleFlowMappings = GetQueryText("flowSync/updateRuleFlowMappings.graphql");
                updateImportControlForFlowSync = GetQueryText("flowSync/updateImportControlForFlowSync.graphql");
                updateFlowMappingsForRemoved = GetQueryText("flowSync/updateFlowMappingsForRemoved.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Flow Queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
