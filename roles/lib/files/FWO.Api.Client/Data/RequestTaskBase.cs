using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{   
    public enum AutoCreateImplTaskOptions
    {
        never, 
        onlyForOneDevice, 
        forEachDevice, 
        enterInReqTask
    }


    public class RequestTaskBase : TaskBase
    {
        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("last_recert_date"), JsonPropertyName("last_recert_date")]
        public DateTime? LastRecertDate { get; set; }

        [JsonProperty("recent_handler"), JsonPropertyName("recent_handler")]
        public UiUser? RecentHandler { get; set; }

        [JsonProperty("assigned_group"), JsonPropertyName("assigned_group")]
        public string? AssignedGroup { get; set; }


        public RequestTaskBase()
        { }

        public RequestTaskBase(RequestTaskBase task) : base(task)
        {
            Title = task.Title;
            TaskNumber = task.TaskNumber;
            RequestAction = task.RequestAction;
            Reason = task.Reason;
            LastRecertDate = task.LastRecertDate;
            RecentHandler = task.RecentHandler;
            AssignedGroup = task.AssignedGroup;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            AssignedGroup = Sanitizer.SanitizeLdapPathOpt(FwAdminComments, ref shortened);
            return shortened;
        }
    }
}
