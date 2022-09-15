using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElement : ElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public long TaskId { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        public Cidr Cidr { get; set; }

        public RequestElement()
        { }

        public RequestElement(RequestElement element) : base (element)
        {
            Id = element.Id;
            TaskId = element.TaskId;
            RequestAction = element.RequestAction;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
        }
    }
}
