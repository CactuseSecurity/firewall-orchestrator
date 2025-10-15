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
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql");
                    
                networkObjectDetailsForVarianceFragment =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetailsForVariance.graphql");

                getNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "networkObject/getNetworkObjectDetails.graphql");

                getNetworkObjectsForManagement =
                    networkObjectDetailsForVarianceFragment +
                    File.ReadAllText(QueryPath + "networkObject/getNetworkObjectsForManagement.graphql");

                networkServiceDetailsFragment =
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql");

                getNetworkServiceDetails =
                    networkServiceDetailsFragment +
                    File.ReadAllText(QueryPath + "networkService/getNetworkServiceDetails.graphql");

                userDetailsFragment = File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql");

                getUserDetails =
                    userDetailsFragment +
                    File.ReadAllText(QueryPath + "user/getUserDetails.graphql");

                // used for right side bar objects
                getAllObjectDetails =
                    userDetailsFragment +
                    networkServiceDetailsFragment +
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "allObjects/getAllObjectDetails.graphql");

                // for rule export and RSB obj filtering per report
                getReportFilteredObjectDetails = 
                    userDetailsFragment +
                    networkServiceDetailsFragment +
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getReportFilteredObjectDetails.graphql");

                getReportFilteredNetworkObjectDetails =
                    networkObjectDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getReportFilteredNetworkObjectDetails.graphql");

                getReportFilteredNetworkServiceDetails =
                    networkServiceDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getReportFilteredNetworkServiceDetails.graphql");

                getReportFilteredUserDetails =
                    userDetailsFragment +
                    File.ReadAllText(QueryPath + "report/getReportFilteredUserDetails.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
