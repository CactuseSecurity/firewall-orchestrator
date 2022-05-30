using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum TaskType
    {
        access, 
        svc_group, 
        obj_group, 
        rule_modify
    }

    public enum RequestAction
    {
        create,
        delete,
        modify
    }

    public class RequestTaskBase
    {
        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = "access";

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = "create";

        [JsonProperty("rule_action"), JsonPropertyName("rule_action")]
        public int? RuleAction { get; set; }

        [JsonProperty("rule_tracking"), JsonPropertyName("rule_tracking")]
        public int? Tracking { get; set; }

        [JsonProperty("start"), JsonPropertyName("start")]
        public DateTime? Start { get; set; }

        [JsonProperty("stop"), JsonPropertyName("stop")]
        public DateTime? Stop { get; set; }

        [JsonProperty("svc_grp_id"), JsonPropertyName("svc_grp_id")]
        public int? ServiceGroupId { get; set; }

        [JsonProperty("nw_obj_grp_id"), JsonPropertyName("nw_obj_grp_id")]
        public int? NetworkGroupId { get; set; }

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }


        public RequestTaskBase()
        { }

        public RequestTaskBase(RequestTaskBase task)
        {
            Title = task.Title;
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
