using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ExtRequestQueries : Queries
    {
        public static readonly string extRequestDetailsFragment;

        public static readonly string addTicketId;
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
                extRequestDetailsFragment = File.ReadAllText(QueryPath + "extRequest/fragments/extRequestDetails.graphql");

                addTicketId = File.ReadAllText(QueryPath + "extRequest/addTicketId.graphql");
                getLatestTicketId = File.ReadAllText(QueryPath + "extRequest/getLatestTicketId.graphql");
                addExtRequest = File.ReadAllText(QueryPath + "extRequest/addExtRequest.graphql");
                getOpenRequests = extRequestDetailsFragment + File.ReadAllText(QueryPath + "extRequest/getOpenRequests.graphql");
                getAndLockOpenRequests = extRequestDetailsFragment + File.ReadAllText(QueryPath + "extRequest/getAndLockOpenRequests.graphql");
                getLastRequest = extRequestDetailsFragment + File.ReadAllText(QueryPath + "extRequest/getLastRequest.graphql");
                updateExtRequestCreation = File.ReadAllText(QueryPath + "extRequest/updateExtRequestCreation.graphql");
                updateExtRequestProcess = File.ReadAllText(QueryPath + "extRequest/updateExtRequestProcess.graphql");
                updateExtRequestFinal = File.ReadAllText(QueryPath + "extRequest/updateExtRequestFinal.graphql");
                updateExternalRequestWaitCycles = File.ReadAllText(QueryPath + "extRequest/updateExternalRequestWaitCycles.graphql");
                updateExternalRequestLock = File.ReadAllText(QueryPath + "extRequest/updateExternalRequestLock.graphql");

                subscribeExtRequestStateUpdate = File.ReadAllText(QueryPath + "extRequest/subscribeExtRequestStateUpdate.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ExtRequestQueries", "Api ExtRequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
