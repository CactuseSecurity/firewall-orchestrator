using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Outcome of saving a new ticket: the saved ticket and whether its initial workflow actions failed.
    /// </summary>
    /// <param name="Ticket">The saved ticket; its id is 0 only if it was not saved.</param>
    /// <param name="InitialActionsFailed">True if the ticket was saved but its initial workflow actions failed.</param>
    /// <param name="ReloadFailed">True if the ticket was saved but could not be read back: <paramref name="Ticket"/> is
    /// then the in-memory copy with the new id but without the ids of its request tasks and must not be processed further.</param>
    public sealed record WfTicketCreationResult(WfTicket Ticket, bool InitialActionsFailed, bool ReloadFailed = false)
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
