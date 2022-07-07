using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    // public enum TaskType
    // {
    //     access, 
    //     svc_group, 
    //     obj_group, 
    //     rule_modify
    // }

    public enum RequestAction
    {
        create,
        delete,
        modify
    }

    public class TaskBase : StatefulObject
    {

        // [JsonProperty("task_number"), JsonPropertyName("task_number")]
        // public int TaskNumber { get; set; }

        // [JsonProperty("task_type"), JsonPropertyName("task_type")]
        // public string TaskType { get; set; } = FWO.Api.Data.TaskType.access.ToString();

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

        [JsonProperty("current_handler"), JsonPropertyName("current_handler")]
        public UiUser? CurrentHandler { get; set; }

        // [JsonProperty("recent_handler"), JsonPropertyName("recent_handler")]
        // public UiUser? RecentHandler { get; set; }

        // [JsonProperty("assigned_group"), JsonPropertyName("assigned_group")]
        // public string? AssignedGroup { get; set; }

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
            // TaskNumber = task.TaskNumber;
            // TaskType = task.TaskType;
            // RequestAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = task.Start;
            Stop = task.Stop;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            UserGroupId = task.UserGroupId;
            CurrentHandler = task.CurrentHandler;
            // RecentHandler = task.RecentHandler;
            // AssignedGroup = task.AssignedGroup;
            TargetBeginDate = task.TargetBeginDate;
            TargetEndDate = task.TargetEndDate;
            FwAdminComments = task.FwAdminComments;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            FwAdminComments = Sanitizer.SanitizeOpt(FwAdminComments, ref shortened);
            // AssignedGroup = Sanitizer.SanitizeLdapPathOpt(FwAdminComments, ref shortened);
            return shortened;
        }
    }
}
