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
        public static readonly string getFlowObjectCatalog;
        public static readonly string getFlowSelectableManagements;

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

                getFlowObjectCatalog =
                    flowNwObjectDetailsFragment +
                    flowNwGroupDetailsFragment +
                    flowSvcObjectDetailsFragment +
                    flowSvcGroupDetailsFragment +
                    flowTimeObjectDetailsFragment +
                    GetQueryText("flow/getFlowObjectCatalog.graphql");

                getFlowSelectableManagements =
                    GetQueryText("flow/getFlowSelectableManagements.graphql");
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
