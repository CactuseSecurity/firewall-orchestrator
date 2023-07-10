using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ObjectQueries : Queries
    {
        public static readonly string networkObjectDetailsFragment;
        public static readonly string getNetworkObjectDetails, getTenantNetworkObjectDetails;
        public static readonly string networkServiceObjectDetailsFragment;
        public static readonly string getNetworkServiceObjectDetails;
        public static readonly string userDetailsFragment;
        public static readonly string getUserDetails;
        public static readonly string getAllObjectDetails, getTenantAllObjectDetails;
        public static readonly string getReportFilteredObjectDetails;
        public static readonly string getReportFilteredNetworkObjectDetails;
        public static readonly string getReportFilteredNetworkServiceObjectDetails;
        public static readonly string getReportFilteredUserDetails;

        static ObjectQueries() 
        {
            try
            {
                networkObjectDetailsFragment =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql");

                getNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "networkObject/getNetworkObjectDetails.graphql");

                getTenantNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "networkObject/getTenantNetworkObjectDetails.graphql");

                networkServiceObjectDetailsFragment =
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql");

                getNetworkServiceObjectDetails =
                    networkServiceObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "networkService/getNetworkServiceDetails.graphql");

                userDetailsFragment = File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql");

                getUserDetails =
                    userDetailsFragment +
                    File.ReadAllText(QueryPath + "user/getUserDetails.graphql");

                // used for right side bar objects
                getAllObjectDetails =
                    userDetailsFragment +
                    networkServiceObjectDetailsFragment +
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "allObjects/getAllObjectDetails.graphql");

                getTenantAllObjectDetails =
                    userDetailsFragment +
                    networkServiceObjectDetailsFragment +
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "allObjects/getTenantAllObjectDetails.graphql");

                // for rule export and RSB obj filtering per report
                getReportFilteredObjectDetails = 
                    userDetailsFragment +
                    networkServiceObjectDetailsFragment +
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getAllObjectDetailsInReport.graphql");

                getReportFilteredNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getNetworkObjectDetailsInReport.graphql");

                getReportFilteredNetworkServiceObjectDetails =
                    networkServiceObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getNetworkServiceDetailsInReport.graphql");

                getReportFilteredUserDetails =
                    userDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getUserDetailsInReport.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
