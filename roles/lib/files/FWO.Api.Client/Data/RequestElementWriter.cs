using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElementWriter : ElementBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();


        public RequestElementWriter()
        { }

        public RequestElementWriter(RequestElement element) : base(element)
        { 
            RequestAction = element.RequestAction;
        }
    }
}
