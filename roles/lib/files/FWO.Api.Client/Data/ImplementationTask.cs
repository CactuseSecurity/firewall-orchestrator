using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImplementationTask
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("request_task_id"), JsonPropertyName("request_task_id")]
        public int ReqTaskId { get; set; }

        [JsonProperty("implementation_task_number"), JsonPropertyName("implementation_task_number")]
        public int ImplTaskNumber { get; set; }

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = "create";

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

        [JsonProperty("user_grp_id "), JsonPropertyName("user_grp_id ")]
        public int? UserGroupId { get; set; }

        [JsonProperty("current_handler"), JsonPropertyName("current_handler")]
        public UiUser? CurrentHandler { get; set; }

        [JsonProperty("target_begin_date"), JsonPropertyName("target_begin_date")]
        public DateTime? TargetBeginDate { get; set; }

        [JsonProperty("target_end_date"), JsonPropertyName("target_end_date")]
        public DateTime? TargetEndDate { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<ImplementationElement> ImplElements { get; set; } = new List<ImplementationElement>();


        public ImplementationTask()
        { }

        public ImplementationTask(RequestTask task)
        {
            Id = 0;
            ReqTaskId = task.Id;
            ImplTaskNumber = 0;
            StateId = 0;
            ImplAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = null;
            Stop = null;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            UserGroupId = task.UserGroupId;
            CurrentHandler = task.CurrentHandler;
            TargetBeginDate = task.TargetBeginDate;
            TargetEndDate = task.TargetEndDate;
            if (task.Elements != null && task.Elements.Count > 0)
            {
                ImplElements = new List<ImplementationElement>();
                foreach(RequestElement element in task.Elements)
                {
                    ImplElements.Add(new ImplementationElement(element));
                }
            }
        }

        public bool Sanitize()
        {
            bool shortened = false;

            return shortened;
        }

    }
}
