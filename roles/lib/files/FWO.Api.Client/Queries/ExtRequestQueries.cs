using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ExtRequestQueries : Queries
    {
        public static readonly string addTicketId;
        public static readonly string getLatestTicketId;
        public static readonly string addExtRequest;
        public static readonly string getOpenRequests;
        public static readonly string updateExtRequestState;

        public static readonly string subscribeExtRequestStateUpdate;


        static ExtRequestQueries()
        {
            try
            {
                addTicketId = File.ReadAllText(QueryPath + "extRequest/addTicketId.graphql");
                getLatestTicketId = File.ReadAllText(QueryPath + "extRequest/getLatestTicketId.graphql");
                addExtRequest = File.ReadAllText(QueryPath + "extRequest/addExtRequest.graphql");
                getOpenRequests = File.ReadAllText(QueryPath + "extRequest/getOpenRequests.graphql");
                updateExtRequestState = File.ReadAllText(QueryPath + "extRequest/updateExtRequestState.graphql");

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
