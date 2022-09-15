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

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("last_recert_date"), JsonPropertyName("last_recert_date")]
        public DateTime? LastRecertDate { get; set; }


        public RequestTaskBase()
        { }

        public RequestTaskBase(RequestTaskBase task) : base(task)
        {
            Title = task.Title;
            RequestAction = task.RequestAction;
            Reason = task.Reason;
            LastRecertDate = task.LastRecertDate;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            return shortened;
        }
    }
}
