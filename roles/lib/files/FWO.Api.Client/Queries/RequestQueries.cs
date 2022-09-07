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
        public static readonly string newApproval;
        public static readonly string updateApproval;
        public static readonly string getStates;
        public static readonly string upsertState;
        public static readonly string deleteState;
        public static readonly string getActions;
        public static readonly string newAction;
        public static readonly string updateAction;
        public static readonly string deleteAction;
        public static readonly string addStateAction;
        public static readonly string removeStateAction;


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
                newApproval = File.ReadAllText(QueryPath + "request/newApproval.graphql");
                updateApproval = File.ReadAllText(QueryPath + "request/updateApproval.graphql");
                getStates = File.ReadAllText(QueryPath + "request/getStates.graphql");
                upsertState = File.ReadAllText(QueryPath + "request/upsertState.graphql");
                deleteState = File.ReadAllText(QueryPath + "request/deleteState.graphql");
                getActions = File.ReadAllText(QueryPath + "request/getActions.graphql");
                newAction = File.ReadAllText(QueryPath + "request/newAction.graphql");
                updateAction = File.ReadAllText(QueryPath + "request/updateAction.graphql");
                deleteAction = File.ReadAllText(QueryPath + "request/deleteAction.graphql");
                addStateAction = File.ReadAllText(QueryPath + "request/addStateAction.graphql");
                removeStateAction = File.ReadAllText(QueryPath + "request/removeStateAction.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize RequestQueries", "Api RequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
