using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Outcome of saving a new ticket: the saved ticket and whether its initial workflow actions failed.
    /// </summary>
    /// <param name="Ticket">The saved ticket; its id is 0 if it could not be saved.</param>
    /// <param name="InitialActionsFailed">True if the ticket was saved but its initial workflow actions failed.</param>
    public sealed record WfTicketCreationResult(WfTicket Ticket, bool InitialActionsFailed)
    {
        /// <summary>Status text of initial workflow actions that all completed.</summary>
        public const string kActionsCompleted = "completed";

        /// <summary>Status text of initial workflow actions that failed.</summary>
        public const string kActionsFailed = "failed";

        /// <summary>
        /// Status text of the initial workflow actions.
        /// </summary>
        public string ActionsStatus => InitialActionsFailed ? kActionsFailed : kActionsCompleted;
    }
}
