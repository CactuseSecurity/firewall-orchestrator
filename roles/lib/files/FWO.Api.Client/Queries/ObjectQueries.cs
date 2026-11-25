using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ObjectQueries : Queries
    {
        public static readonly string networkObjectDetailsFragment;
        public static readonly string networkObjectDetailsForVarianceFragment;
        public static readonly string getNetworkObjectDetails;
        public static readonly string getNetworkObjectsForManagement;
        public static readonly string getImportedObjectsOfTypes;
        public static readonly string networkServiceDetailsFragment;
        public static readonly string getNetworkServiceDetails;
        public static readonly string userDetailsFragment;
        public static readonly string getUserDetails;
        public static readonly string getAllObjectDetails;
        public static readonly string getReportFilteredObjectDetails;
        public static readonly string getReportFilteredNetworkObjectDetails;
        public static readonly string getReportFilteredNetworkServiceDetails;
        public static readonly string getReportFilteredUserDetails;
        public static readonly string getNetworkObjectDetailsByIds;
        public static readonly string getNetworkServiceDetailsByIds;
        public static readonly string getUserDetailsByIds;
        public static readonly string searchNetworkObjectsForRequestByName;
        public static readonly string searchNetworkObjectsForRequestByIp;
        public static readonly string searchNetworkGroupsForRequestByName;
        public static readonly string getNetworkGroupMembersForRequest;
        public static readonly string searchNetworkServicesForRequestByName;
        public static readonly string searchNetworkServicesForRequestByPort;
        public static readonly string searchNetworkServiceGroupsForRequestByName;
        public static readonly string getServiceGroupMembersForRequest;
        public static readonly string searchTimeObjectsForRequestByName;

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

                // imported objects of given types, e.g. the access roles a modelling placeholder may be substituted by
                getImportedObjectsOfTypes =
                    GetQueryText("networkObject/getImportedObjectsOfTypes.graphql");

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

                // RSB report tab of change reports: the object versions referenced by the reported changes
                getNetworkObjectDetailsByIds =
                    networkObjectDetailsFragment +
                    GetQueryText("networkObject/getNetworkObjectDetailsByIds.graphql");

                getNetworkServiceDetailsByIds =
                    networkServiceDetailsFragment +
                    GetQueryText("networkService/getNetworkServiceDetailsByIds.graphql");

                getUserDetailsByIds =
                    userDetailsFragment +
                    GetQueryText("user/getUserDetailsByIds.graphql");

                // lean searches offering a single object or service to the object_modify request task
                searchNetworkObjectsForRequestByName =
                    GetQueryText("networkObject/searchNetworkObjectsForRequestByName.graphql");
                searchNetworkObjectsForRequestByIp =
                    GetQueryText("networkObject/searchNetworkObjectsForRequestByIp.graphql");
                searchNetworkGroupsForRequestByName =
                    GetQueryText("networkObject/searchNetworkGroupsForRequestByName.graphql");
                getNetworkGroupMembersForRequest =
                    GetQueryText("networkObject/getNetworkGroupMembersForRequest.graphql");
                searchNetworkServicesForRequestByName =
                    GetQueryText("networkService/searchNetworkServicesForRequestByName.graphql");
                searchNetworkServicesForRequestByPort =
                    GetQueryText("networkService/searchNetworkServicesForRequestByPort.graphql");
                searchNetworkServiceGroupsForRequestByName =
                    GetQueryText("networkService/searchNetworkServiceGroupsForRequestByName.graphql");
                getServiceGroupMembersForRequest =
                    GetQueryText("networkService/getServiceGroupMembersForRequest.graphql");
                searchTimeObjectsForRequestByName =
                    GetQueryText("time/searchTimeObjectsForRequestByName.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
