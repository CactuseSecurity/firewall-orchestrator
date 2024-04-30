using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestReqElementWriter : RequestElementBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = Data.RequestAction.create.ToString();

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        public RequestReqElementWriter()
        {}

        public RequestReqElementWriter(RequestReqElement element) : base(element)
        { 
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            IpString = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null;
        }
    }
}
