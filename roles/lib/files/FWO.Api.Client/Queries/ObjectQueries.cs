using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ObjectQueries : Queries
    {
        public static readonly string networkObjectDetailsFragment;
        public static readonly string networkObjectDetailsForVarianceFragment;
        public static readonly string getNetworkObjectDetails;
        public static readonly string getNetworkObjectsForManagement;
        public static readonly string networkServiceDetailsFragment;
        public static readonly string getNetworkServiceDetails;
        public static readonly string userDetailsFragment;
        public static readonly string getUserDetails;
        public static readonly string getAllObjectDetails;
        public static readonly string getReportFilteredObjectDetails;
        public static readonly string getReportFilteredNetworkObjectDetails;
        public static readonly string getReportFilteredNetworkServiceDetails;
        public static readonly string getReportFilteredUserDetails;

        static ObjectQueries()
        {
            try
            {
                networkObjectDetailsFragment =
                    GetQueryText("networkObject/fragments/networkObjectDetails.graphql");

                networkObjectDetailsForVarianceFragment =
                    GetQueryText("networkObject/fragments/networkObjectDetailsForVariance.graphql");

                getNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    GetQueryText("networkObject/getNetworkObjectDetails.graphql");

                getNetworkObjectsForManagement =
                    networkObjectDetailsForVarianceFragment +
                    GetQueryText("networkObject/getNetworkObjectsForManagement.graphql");

                networkServiceDetailsFragment =
                    GetQueryText("networkService/fragments/networkServiceDetails.graphql");

                getNetworkServiceDetails =
                    networkServiceDetailsFragment +
                    GetQueryText("networkService/getNetworkServiceDetails.graphql");

                userDetailsFragment = GetQueryText("user/fragments/userDetails.graphql");

                getUserDetails =
                    userDetailsFragment +
                    GetQueryText("user/getUserDetails.graphql");

                // used for right side bar objects
                getAllObjectDetails =
                    userDetailsFragment +
                    networkServiceDetailsFragment +
                    networkObjectDetailsFragment +
                    GetQueryText("allObjects/getAllObjectDetails.graphql");

                // for rule export and RSB obj filtering per report
                getReportFilteredObjectDetails =
                    userDetailsFragment +
                    networkServiceDetailsFragment +
                    networkObjectDetailsFragment +
                    GetQueryText("report/getReportFilteredObjectDetails.graphql");

                getReportFilteredNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    GetQueryText("report/getReportFilteredNetworkObjectDetails.graphql");

                getReportFilteredNetworkServiceDetails =
                    networkServiceDetailsFragment +
                    GetQueryText("report/getReportFilteredNetworkServiceDetails.graphql");

                getReportFilteredUserDetails =
                    userDetailsFragment +
                    GetQueryText("report/getReportFilteredUserDetails.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
