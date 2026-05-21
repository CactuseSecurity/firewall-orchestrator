using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowTimeObject
    {
        [JsonProperty("timeobj_id"), JsonPropertyName("timeobj_id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("end_time"), JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }

        [JsonProperty("timeobj_hash"), JsonPropertyName("timeobj_hash")]
        public string Hash { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        [JsonProperty("time_objects"), JsonPropertyName("time_objects")]
        public List<TimeObject>? TimeObjects { get; set; }
    }

    public class FlowTimeObjectInsertResult
    {
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<FlowTimeObject> Returning { get; set; } = [];
    }
}
