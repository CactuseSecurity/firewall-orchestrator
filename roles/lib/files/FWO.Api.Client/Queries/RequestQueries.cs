using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RequestQueries : Queries
    {
        public static readonly string commentDetailsFragment;
        public static readonly string implTaskDetailsFragment;
        public static readonly string reqTaskDetailsFragment;
        public static readonly string reqElementDetailsFragment;
        public static readonly string ticketDetailsFragment;
        public static readonly string reqTaskOverviewFragment;
        public static readonly string ticketOverviewFragment;
        public static readonly string ticketDetailsReqTaskOverviewFragment;

        public static readonly string getTickets;
        public static readonly string getFullTickets;
        public static readonly string getOwnerTicketIds;
        public static readonly string getTicketById;
        public static readonly string newTicket;
        public static readonly string updateTicket;
        public static readonly string updateTicketState;
        public static readonly string subscribeTicketStateChanges;
        public static readonly string subscribeTaskChanges;
        public static readonly string newRequestTask;
        public static readonly string updateRequestTask;
        public static readonly string updateRequestTaskState;
        public static readonly string updateRequestTaskAdditionalInfo;
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
        public static readonly string getExtStates;
        public static readonly string addExtState;
        public static readonly string removeExtState;
        public static readonly string getActions;
        public static readonly string newAction;
        public static readonly string updateAction;
        public static readonly string deleteAction;
        public static readonly string addStateAction;
        public static readonly string removeStateAction;
        public static readonly string newComment;
        public static readonly string addCommentToReqTask;
        public static readonly string addCommentToImplTask;
        public static readonly string addCommentToTicket;
        public static readonly string addCommentToApproval;
        public static readonly string addOwnerToReqTask;
        public static readonly string removeOwnerFromReqTask;


        static RequestQueries()
        {
            try
            {
                commentDetailsFragment = GetQueryText("request/fragments/commentDetails.graphql");
                implTaskDetailsFragment = commentDetailsFragment + GetQueryText("request/fragments/implTaskDetails.graphql");
                reqElementDetailsFragment = GetQueryText("request/fragments/reqElementDetails.graphql");
                reqTaskDetailsFragment = OwnerQueries.ownerDetailsFragment + reqElementDetailsFragment + implTaskDetailsFragment + GetQueryText("request/fragments/reqTaskDetails.graphql");
                ticketDetailsFragment = reqTaskDetailsFragment + GetQueryText("request/fragments/ticketDetails.graphql");
                reqTaskOverviewFragment = OwnerQueries.ownerDetailsFragment + GetQueryText("request/fragments/reqTaskOverview.graphql");
                ticketOverviewFragment = reqTaskOverviewFragment + GetQueryText("request/fragments/ticketOverview.graphql");
                ticketDetailsReqTaskOverviewFragment = commentDetailsFragment + reqTaskOverviewFragment + GetQueryText("request/fragments/ticketDetailsReqTaskOverview.graphql");

                getTickets = ticketDetailsReqTaskOverviewFragment + GetQueryText("request/getTickets.graphql");
                getFullTickets = ticketDetailsFragment + GetQueryText("request/getFullTickets.graphql");
                getOwnerTicketIds = GetQueryText("monitor/getOwnerTicketIds.graphql");
                getTicketById = ticketDetailsFragment + GetQueryText("request/getTicketById.graphql");
                newTicket = GetQueryText("request/newTicket.graphql");
                updateTicket = GetQueryText("request/updateTicket.graphql");
                updateTicketState = GetQueryText("request/updateTicketState.graphql");
                subscribeTicketStateChanges = GetQueryText("request/subscribeTicketStateChanges.graphql");
                subscribeTaskChanges = reqElementDetailsFragment + GetQueryText("request/subscribeTaskChanges.graphql");
                newRequestTask = GetQueryText("request/newRequestTask.graphql");
                updateRequestTask = GetQueryText("request/updateRequestTask.graphql");
                updateRequestTaskState = GetQueryText("request/updateRequestTaskState.graphql");
                updateRequestTaskAdditionalInfo = GetQueryText("request/updateRequestTaskAdditionalInfo.graphql");
                deleteRequestTask = GetQueryText("request/deleteRequestTask.graphql");
                newRequestElement = GetQueryText("request/newRequestElement.graphql");
                updateRequestElement = GetQueryText("request/updateRequestElement.graphql");
                deleteRequestElement = GetQueryText("request/deleteRequestElement.graphql");
                newImplementationTask = GetQueryText("request/newImplementationTask.graphql");
                updateImplementationTask = GetQueryText("request/updateImplementationTask.graphql");
                updateImplementationTaskState = GetQueryText("request/updateImplementationTaskState.graphql");
                deleteImplementationTask = GetQueryText("request/deleteImplementationTask.graphql");
                newImplementationElement = GetQueryText("request/newImplementationElement.graphql");
                updateImplementationElement = GetQueryText("request/updateImplementationElement.graphql");
                deleteImplementationElement = GetQueryText("request/deleteImplementationElement.graphql");
                newApproval = GetQueryText("request/newApproval.graphql");
                updateApproval = GetQueryText("request/updateApproval.graphql");
                getStates = GetQueryText("request/getStates.graphql");
                upsertState = GetQueryText("request/upsertState.graphql");
                deleteState = GetQueryText("request/deleteState.graphql");
                getExtStates = GetQueryText("request/getExtStates.graphql");
                addExtState = GetQueryText("request/addExtState.graphql");
                removeExtState = GetQueryText("request/removeExtState.graphql");
                getActions = GetQueryText("request/getActions.graphql");
                newAction = GetQueryText("request/newAction.graphql");
                updateAction = GetQueryText("request/updateAction.graphql");
                deleteAction = GetQueryText("request/deleteAction.graphql");
                addStateAction = GetQueryText("request/addStateAction.graphql");
                removeStateAction = GetQueryText("request/removeStateAction.graphql");
                newComment = GetQueryText("request/newComment.graphql");
                addCommentToReqTask = GetQueryText("request/addCommentToReqTask.graphql");
                addCommentToImplTask = GetQueryText("request/addCommentToImplTask.graphql");
                addCommentToTicket = GetQueryText("request/addCommentToTicket.graphql");
                addCommentToApproval = GetQueryText("request/addCommentToApproval.graphql");
                addOwnerToReqTask = GetQueryText("request/addOwnerToReqTask.graphql");
                removeOwnerFromReqTask = GetQueryText("request/removeOwnerFromReqTask.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize RequestQueries", "Api RequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
