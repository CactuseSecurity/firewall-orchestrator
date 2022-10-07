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


    public class RequestReqTaskBase : RequestTaskBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("last_recert_date"), JsonPropertyName("last_recert_date")]
        public DateTime? LastRecertDate { get; set; }

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public string? Devices { get; set; }


        public RequestReqTaskBase()
        { }

        public RequestReqTaskBase(RequestReqTaskBase reqtask) : base(reqtask)
        {
            RequestAction = reqtask.RequestAction;
            Reason = reqtask.Reason;
            LastRecertDate = reqtask.LastRecertDate;
            Devices = reqtask.Devices;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            return shortened;
        }
    }
}
