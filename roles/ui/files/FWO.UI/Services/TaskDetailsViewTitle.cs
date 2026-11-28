using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>Builds the popup title of the approval and planning views.</summary>
    public static class TaskDetailsViewTitle
    {
        /// <summary>Returns the ticket headline followed by id, state and, if known, requester of the ticket.</summary>
        /// <param name="userConfig">The user configuration providing the texts and the pre-workflow reference label.</param>
        /// <param name="ticket">The displayed ticket.</param>
        /// <param name="states">The workflow state names.</param>
        public static string Build(UserConfig userConfig, WfTicket ticket, WfStateDict states)
        {
            string requester = ticket.Requester?.Name ?? "";
            string metadata = $"{userConfig.GetText("id")}: {ticket.Id}, {userConfig.GetText("state")}: {states.GetName(ticket.StateId)}";
            if (!string.IsNullOrWhiteSpace(requester))
            {
                metadata += $", {userConfig.GetText("requester")}: {requester}";
            }
            string headline = PreWorkflowTicketReferenceDisplay.TicketHeadline(userConfig.GetText("ticket"), userConfig.ReqPreWorkflowTicketReferenceLabel, ticket);
            return $"{headline} ({metadata})";
        }
    }
}
