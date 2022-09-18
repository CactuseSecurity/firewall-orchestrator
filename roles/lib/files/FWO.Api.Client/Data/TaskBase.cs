using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum TaskType
    {
        master,
        generic,
        access, 
        group, // obj_group ??
        //svc_group, own task type or just group??
        //delete,
        //rule_modify
    }

    public enum RequestAction
    {
        create,
        delete,
        modify
    }

    public class TaskBase : StatefulObject
    {
        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = FWO.Api.Data.TaskType.access.ToString();

        // [JsonProperty("request_action"), JsonPropertyName("request_action")]
        // public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

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

        [JsonProperty("fw_admin_comments"), JsonPropertyName("fw_admin_comments")]
        public string? FwAdminComments { get; set; }


        public TaskBase()
        { }

        public TaskBase(TaskBase task) : base(task)
        {
            DeviceId = task.DeviceId;
            TaskNumber = task.TaskNumber;
            TaskType = task.TaskType;
            // RequestAction = task.RequestAction;
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
            FwAdminComments = task.FwAdminComments;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            FreeText = Sanitizer.SanitizeOpt(FreeText, ref shortened);
            FwAdminComments = Sanitizer.SanitizeOpt(FwAdminComments, ref shortened);
            return shortened;
        }
    }
}
