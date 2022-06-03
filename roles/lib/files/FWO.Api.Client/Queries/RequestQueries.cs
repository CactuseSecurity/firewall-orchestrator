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
        public static readonly string newImplementationTask;
        public static readonly string updateImplementationTask;
        public static readonly string updateImplementationTaskState;
        public static readonly string deleteImplementationTask;
        public static readonly string newImplementationElement;
        public static readonly string updateImplementationElement;
        public static readonly string deleteImplementationElement;


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
                newImplementationTask = File.ReadAllText(QueryPath + "request/newImplementationTask.graphql");
                updateImplementationTask = File.ReadAllText(QueryPath + "request/updateImplementationTask.graphql");
                updateImplementationTaskState = File.ReadAllText(QueryPath + "request/updateImplementationTaskState.graphql");
                deleteImplementationTask = File.ReadAllText(QueryPath + "request/deleteImplementationTask.graphql");
                newImplementationElement = File.ReadAllText(QueryPath + "request/newImplementationElement.graphql");
                updateImplementationElement = File.ReadAllText(QueryPath + "request/updateImplementationElement.graphql");
                deleteImplementationElement = File.ReadAllText(QueryPath + "request/deleteImplementationElement.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize RequestQueries", "Api RequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
