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

    internal static class WorkflowReferenceDateSerialization
    {
        public static WorkflowReferenceDate Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return WorkflowReferenceDate.AnyActivity;
            }

            if (Enum.TryParse(value.Trim(), true, out WorkflowReferenceDate referenceDate))
            {
                return referenceDate;
            }

            throw new JsonException($"Unknown workflow reference date '{value}'.");
        }

        public static string Format(WorkflowReferenceDate value)
        {
            return value.ToString();
        }
    }

    public enum WorkflowLabelFilterMode
    {
        not_existing,
        existing,
        value
    }

    public class WorkflowLabelFilter
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("mode"), JsonPropertyName("mode")]
        public WorkflowLabelFilterMode Mode { get; set; } = WorkflowLabelFilterMode.existing;

        [JsonProperty("value"), JsonPropertyName("value")]
        public string Value { get; set; } = "";

        public WorkflowLabelFilter()
        { }

        public WorkflowLabelFilter(WorkflowLabelFilter workflowLabelFilter)
        {
            Name = workflowLabelFilter.Name;
            Mode = workflowLabelFilter.Mode;
            Value = workflowLabelFilter.Value;
        }
    }

    public class WorkflowFilter
    {
        [JsonProperty("reference_date"), JsonPropertyName("reference_date")]
        public string ReferenceDateRaw { get; set; } = WorkflowReferenceDateSerialization.Format(WorkflowReferenceDate.AnyActivity);

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public WorkflowReferenceDate ReferenceDate
        {
            get => WorkflowReferenceDateSerialization.Parse(ReferenceDateRaw);
            set => ReferenceDateRaw = WorkflowReferenceDateSerialization.Format(value);
        }

        [JsonProperty("task_types"), JsonPropertyName("task_types")]
        public List<WfTaskType> TaskTypes { get; set; } = DefaultTaskTypes();

        [JsonProperty("state_ids"), JsonPropertyName("state_ids")]
        public List<int> StateIds { get; set; } = [];

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonProperty("label_filter"), JsonPropertyName("label_filter")]
        public WorkflowLabelFilter LabelFilter { get; set; } = new();

        [JsonProperty("show_full_ticket"), JsonPropertyName("show_full_ticket")]
        public bool ShowFullTicket { get; set; } = true;

        public WorkflowFilter()
        { }

        public WorkflowFilter(WorkflowFilter workflowFilter)
        {
            ReferenceDate = workflowFilter.ReferenceDate;
            TaskTypes = workflowFilter.TaskTypes.Count > 0 ? [.. workflowFilter.TaskTypes] : DefaultTaskTypes();
            StateIds = [.. workflowFilter.StateIds];
            Phase = workflowFilter.Phase;
            LabelFilter = new(workflowFilter.LabelFilter);
            ShowFullTicket = workflowFilter.ShowFullTicket;
        }

        private static List<WfTaskType> DefaultTaskTypes()
        {
            return [.. Enum.GetValues(typeof(WfTaskType)).Cast<WfTaskType>().Where(taskType => taskType != WfTaskType.master)];
        }
    }
}
