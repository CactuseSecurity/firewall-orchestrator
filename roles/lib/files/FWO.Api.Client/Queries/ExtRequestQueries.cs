using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ExtRequestQueries : Queries
    {
        public static readonly string addTicketId;
        public static readonly string getLatestTicketId;
        public static readonly string addExtRequest;
        public static readonly string getOpenRequests;
        public static readonly string getLastRequest;
        public static readonly string updateExtRequestCreation;
        public static readonly string updateExtRequestProcess;
        public static readonly string updateExtRequestFinal;

        public static readonly string subscribeExtRequestStateUpdate;


        static ExtRequestQueries()
        {
            try
            {
                addTicketId = File.ReadAllText(QueryPath + "extRequest/addTicketId.graphql");
                getLatestTicketId = File.ReadAllText(QueryPath + "extRequest/getLatestTicketId.graphql");
                addExtRequest = File.ReadAllText(QueryPath + "extRequest/addExtRequest.graphql");
                getOpenRequests = File.ReadAllText(QueryPath + "extRequest/getOpenRequests.graphql");
                getLastRequest = File.ReadAllText(QueryPath + "extRequest/getLastRequest.graphql");
                updateExtRequestCreation = File.ReadAllText(QueryPath + "extRequest/updateExtRequestCreation.graphql");
                updateExtRequestProcess = File.ReadAllText(QueryPath + "extRequest/updateExtRequestProcess.graphql");
                updateExtRequestFinal = File.ReadAllText(QueryPath + "extRequest/updateExtRequestFinal.graphql");

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
