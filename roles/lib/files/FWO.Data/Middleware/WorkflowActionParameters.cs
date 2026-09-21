using FWO.Data.Workflow;

namespace FWO.Data.Middleware
{
    public class WorkflowActionParameters
    {
        public string Scope { get; set; } = WfObjectScopes.None.ToString();
        public int ActionId { get; set; }
        public long ObjectId { get; set; }
        public long TicketId { get; set; }
        public int OldStateId { get; set; }
        public int NewStateId { get; set; }
        public bool StateChangedByCreation { get; set; }
        public string Phase { get; set; } = WorkflowPhases.request.ToString();
        public string ExecutionMode { get; set; } = "";
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
