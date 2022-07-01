using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElement : ElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public int TaskId { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        public RequestElement()
        { }

        public RequestElement(RequestElement element) : base (element)
        {
            RequestAction = element.RequestAction;
            Id = element.Id;
            TaskId = element.TaskId;
        }
    }
}
