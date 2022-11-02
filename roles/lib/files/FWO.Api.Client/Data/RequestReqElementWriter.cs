using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestReqElementWriter : RequestElementBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();


        public RequestReqElementWriter()
        {}

        public RequestReqElementWriter(RequestReqElement element) : base(element)
        { 
            RequestAction = element.RequestAction;
            if(element.Cidr != null)
            {
                CidrString = element.Cidr.CidrString;
            }
        }
    }
}
