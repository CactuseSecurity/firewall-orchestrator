using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTask
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public int TicketId { get; set; }

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public int TaskType { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public int RequestAction { get; set; }

        [JsonProperty("rule_action"), JsonPropertyName("rule_action")]
        public int? RuleAction { get; set; }

        [JsonProperty("tracking"), JsonPropertyName("tracking")]
        public int? Tracking { get; set; }

        [JsonProperty("start"), JsonPropertyName("start")]
        public DateTime? Start { get; set; }

        [JsonProperty("stop"), JsonPropertyName("stop")]
        public DateTime? Stop { get; set; }

        [JsonProperty("svc_grp_id"), JsonPropertyName("svc_grp_id")]
        public int? ServiceGroupId { get; set; }

        [JsonProperty("nw_grp_id"), JsonPropertyName("nw_grp_id")]
        public int? NetworkGroupId { get; set; }

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestElement> Elements { get; set; } = new List<RequestElement>();

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<ImplementationTask> ImplementationTasks { get; set; } = new List<ImplementationTask>();

        public RequestTask()
        { }

        public RequestTask(RequestTask task)
        {
            Id = task.Id;
            Title = task.Title;
            TicketId = task.TicketId;
            TaskNumber = task.TaskNumber;
            StateId = task.StateId;
            TaskType = task.TaskType;
            RequestAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = task.Start;
            Stop = task.Stop;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            Reason = task.Reason;
            Elements = task.Elements;
            ImplementationTasks = task.ImplementationTasks;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            return shortened;
        }
    }
}
