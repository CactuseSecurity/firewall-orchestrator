using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class NetworkZoneQueries : Queries
    {
        public static readonly string addNetworkZone;
        public static readonly string addPathItemsRoot;
        public static readonly string addPathItemsInternet;
        public static readonly string removeNetworkZone;
        public static readonly string getNetworkZonesForMatrix;
        public static readonly string getIpRangesForMatrix;
        public static readonly string updateNetworkZone;
        public static readonly string deleteNetworkZoneDeviceIpRangeRoot;
        public static readonly string deleteNetworkZoneDeviceIpRangeInternet;

        static NetworkZoneQueries()
        {
            try
            {
                addNetworkZone = GetQueryText("networkZone/addNetworkZone.graphql");
                addPathItemsRoot = GetQueryText("networkZone/addPathItemsRoot.graphql");
                addPathItemsInternet = GetQueryText("networkZone/addPathItemsInternet.graphql");
                removeNetworkZone = GetQueryText("networkZone/removeNetworkZone.graphql");
                getNetworkZonesForMatrix = GetQueryText("networkZone/getNetworkZonesForMatrix.graphql");
                getIpRangesForMatrix = GetQueryText("networkZone/getIpRangesForMatrix.graphql");
                updateNetworkZone = GetQueryText("networkZone/updateNetworkZone.graphql");
                deleteNetworkZoneDeviceIpRangeRoot = GetQueryText("networkZone/deleteNetworkZoneDeviceIpRangeRoot.graphql");
                deleteNetworkZoneDeviceIpRangeInternet = GetQueryText("networkZone/deleteNetworkZoneDeviceIpRangeInternet.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Network Zone Queries", "Api network zone queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
