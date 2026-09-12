using FWO.Data.Workflow;

namespace FWO.Data.Middleware
{
    /// <summary>
    /// Parameters passed from the UI workflow handler to the middleware workflow handler.
    /// </summary>
    public class WorkflowActionParameters
    {
        /// <summary>
        /// Workflow scope of the stateful object.
        /// </summary>
        public string Scope { get; set; } = WfObjectScopes.None.ToString();

        /// <summary>
        /// Identifier of the workflow action to execute.
        /// </summary>
        public int ActionId { get; set; }

        /// <summary>
        /// Identifier of the stateful workflow object.
        /// </summary>
        public long ObjectId { get; set; }

        /// <summary>
        /// Identifier of the related workflow ticket.
        /// </summary>
        public long TicketId { get; set; }

        /// <summary>
        /// State before the workflow transition.
        /// </summary>
        public int OldStateId { get; set; }

        /// <summary>
        /// State after the workflow transition.
        /// </summary>
        public int NewStateId { get; set; }

        /// <summary>
        /// Indicates that the state transition happened while creating the object.
        /// </summary>
        public bool StateChangedByCreation { get; set; }

        /// <summary>
        /// Current workflow phase.
        /// </summary>
        public string Phase { get; set; } = WorkflowPhases.request.ToString();

        /// <summary>
        /// Workflow action execution mode.
        /// </summary>
        public string ExecutionMode { get; set; } = "";

        /// <summary>
        /// Caller-provided notification placeholder values.
        /// </summary>
        public NotificationPlaceholderData? NotificationPlaceholders { get; set; }

        /// <summary>
        /// Id of the workflow email bundle this action belongs to, empty when the action is not bundled.
        /// </summary>
        public string EmailBundleId { get; set; } = "";

        /// <summary>
        /// True when the request only sends the captured emails of a bundle and executes no action.
        /// </summary>
        public bool EmailBundleFlushOnly { get; set; }
    }

    public class WorkflowActionResult
    {
        public bool Success { get; set; }
        public List<WorkflowActionMessage> Messages { get; set; } = [];
        public string ErrorMessage { get; set; } = "";
    }

    public class WorkflowActionMessage
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public bool ErrorFlag { get; set; }
    }
}
