using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.Data.Workflow;

namespace FWO.Data.Report
{
    public enum WorkflowReferenceDate
    {
        TicketCreation,
        TicketClosure,
        ApprovalOpened,
        Approved,
        TaskStart,
        TaskEnd,
        ImplementationStart,
        ImplementationEnd,
        AnyActivity
    }

    public class WorkflowFilter
    {
        [JsonProperty("reference_date"), JsonPropertyName("reference_date")]
        public WorkflowReferenceDate ReferenceDate { get; set; } = WorkflowReferenceDate.TicketCreation;

        [JsonProperty("task_types"), JsonPropertyName("task_types")]
        public List<WfTaskType> TaskTypes { get; set; } = DefaultTaskTypes();

        [JsonProperty("state_ids"), JsonPropertyName("state_ids")]
        public List<int> StateIds { get; set; } = [];

        [JsonProperty("show_full_ticket"), JsonPropertyName("show_full_ticket")]
        public bool ShowFullTicket { get; set; } = true;

        public WorkflowFilter()
        { }

        public WorkflowFilter(WorkflowFilter workflowFilter)
        {
            ReferenceDate = workflowFilter.ReferenceDate;
            TaskTypes = workflowFilter.TaskTypes.Count > 0 ? [.. workflowFilter.TaskTypes] : DefaultTaskTypes();
            StateIds = [.. workflowFilter.StateIds];
            ShowFullTicket = workflowFilter.ShowFullTicket;
        }

        private static List<WfTaskType> DefaultTaskTypes()
        {
            return [.. Enum.GetValues(typeof(WfTaskType)).Cast<WfTaskType>().Where(taskType => taskType != WfTaskType.master)];
        }
    }
}
