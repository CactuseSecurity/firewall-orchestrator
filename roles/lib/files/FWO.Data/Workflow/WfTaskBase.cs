using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public enum WfTaskType
    {
        master = 0,
        generic = 1,
        access = 2, 
        rule_delete = 3,
        rule_modify = 4,
        group_create = 5,
        group_modify = 6,
        group_delete = 7,
        new_interface = 11
    }

    public enum RequestAction
    {
        create,
        delete,
        modify,
        unchanged,
        addAfterCreation
    }

    public class WfTaskBase : WfStatefulObject
    {
        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = WfTaskType.access.ToString();

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

        [JsonProperty("user_grp_id"), JsonPropertyName("user_grp_id")]
        public int? UserGroupId { get; set; }

        [JsonProperty("free_text"), JsonPropertyName("free_text")]
        public string? FreeText { get; set; }

        [JsonProperty("target_begin_date"), JsonPropertyName("target_begin_date")]
        public DateTime? TargetBeginDate { get; set; }

        [JsonProperty("target_end_date"), JsonPropertyName("target_end_date")]
        public DateTime? TargetEndDate { get; set; }


        public WfTaskBase()
        { }

        public WfTaskBase(WfTaskBase task) : base(task)
        {
            Title = task.Title;
            TaskNumber = task.TaskNumber;
            TaskType = task.TaskType;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = task.Start;
            Stop = task.Stop;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            UserGroupId = task.UserGroupId;
            FreeText = task.FreeText;
            TargetBeginDate = task.TargetBeginDate;
            TargetEndDate = task.TargetEndDate;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            FreeText = Sanitizer.SanitizeOpt(FreeText, ref shortened);
            return shortened;
        }
    }
}
