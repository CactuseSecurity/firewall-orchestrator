using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElement : RequestElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public int TaskId { get; set; }

        public RequestElement()
        { }

        public RequestElement(RequestElement element) : base (element)
        {
            Id = element.Id;
            TaskId = element.TaskId;
        }
    }
}
