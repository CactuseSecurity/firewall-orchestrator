using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RequestQueries : Queries
    {
        public static readonly string getTickets;
        public static readonly string newTicket;
        public static readonly string updateTicket;
        public static readonly string updateTicketState;
        public static readonly string newRequestTask;
        public static readonly string updateRequestTask;
        public static readonly string updateRequestTaskState;
        public static readonly string deleteRequestTask;
        public static readonly string newRequestElement;
        public static readonly string updateRequestElement;
        public static readonly string deleteRequestElement;


        static RequestQueries()
        {
            try
            {
                getTickets = File.ReadAllText(QueryPath + "request/getTickets.graphql");
                newTicket = File.ReadAllText(QueryPath + "request/newTicket.graphql");
                updateTicket = File.ReadAllText(QueryPath + "request/updateTicket.graphql");
                updateTicketState = File.ReadAllText(QueryPath + "request/updateTicketState.graphql");
                newRequestTask = File.ReadAllText(QueryPath + "request/newRequestTask.graphql");
                updateRequestTask = File.ReadAllText(QueryPath + "request/updateRequestTask.graphql");
                updateRequestTaskState = File.ReadAllText(QueryPath + "request/updateRequestTaskState.graphql");
                deleteRequestTask = File.ReadAllText(QueryPath + "request/deleteRequestTask.graphql");
                newRequestElement = File.ReadAllText(QueryPath + "request/newRequestElement.graphql");
                updateRequestElement = File.ReadAllText(QueryPath + "request/updateRequestElement.graphql");
                deleteRequestElement = File.ReadAllText(QueryPath + "request/deleteRequestElement.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize RequestQueries", "Api RequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
