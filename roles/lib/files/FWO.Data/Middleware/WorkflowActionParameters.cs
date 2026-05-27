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
