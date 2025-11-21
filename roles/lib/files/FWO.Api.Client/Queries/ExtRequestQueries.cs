using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ExtRequestQueries : Queries
    {
        public static readonly string extRequestDetailsFragment;

        public static readonly string addTicketId;
        public static readonly string getLatestTicketIds;
        public static readonly string getLatestTicketId;
        public static readonly string addExtRequest;
        public static readonly string getOpenRequests;
        public static readonly string getAndLockOpenRequests;
        public static readonly string getLastRequest;
        public static readonly string updateExtRequestCreation;
        public static readonly string updateExtRequestProcess;
        public static readonly string updateExtRequestFinal;
        public static readonly string updateExternalRequestWaitCycles;
        public static readonly string updateExternalRequestLock;

        public static readonly string subscribeExtRequestStateUpdate;


        static ExtRequestQueries()
        {
            try
            {
                extRequestDetailsFragment = GetQueryText("extRequest/fragments/extRequestDetails.graphql");

                addTicketId = GetQueryText("extRequest/addTicketId.graphql");
                getLatestTicketIds = GetQueryText("extRequest/getLatestTicketIds.graphql");
                getLatestTicketId = GetQueryText("extRequest/getLatestTicketId.graphql");
                addExtRequest = GetQueryText("extRequest/addExtRequest.graphql");
                getOpenRequests = extRequestDetailsFragment + GetQueryText("extRequest/getOpenRequests.graphql");
                getAndLockOpenRequests = extRequestDetailsFragment + GetQueryText("extRequest/getAndLockOpenRequests.graphql");
                getLastRequest = extRequestDetailsFragment + GetQueryText("extRequest/getLastRequest.graphql");
                updateExtRequestCreation = GetQueryText("extRequest/updateExtRequestCreation.graphql");
                updateExtRequestProcess = GetQueryText("extRequest/updateExtRequestProcess.graphql");
                updateExtRequestFinal = GetQueryText("extRequest/updateExtRequestFinal.graphql");
                updateExternalRequestWaitCycles = GetQueryText("extRequest/updateExternalRequestWaitCycles.graphql");
                updateExternalRequestLock = GetQueryText("extRequest/updateExternalRequestLock.graphql");

                subscribeExtRequestStateUpdate = GetQueryText("extRequest/subscribeExtRequestStateUpdate.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ExtRequestQueries", "Api ExtRequestQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
