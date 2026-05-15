using System.Text.Json.Serialization;
using FWO.Data.Flow;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class TimeObject
    {
        [JsonProperty("time_obj_id"), JsonPropertyName("time_obj_id")]
        public long Id { get; set; }

        [JsonProperty("time_obj_name"), JsonPropertyName("time_obj_name")]
        public string Name { get; set; } = "";

        [JsonProperty("time_obj_uid"), JsonPropertyName("time_obj_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("end_time"), JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonProperty("flow_timeobj_id"), JsonPropertyName("flow_timeobj_id")]
        public long? FlowTimeObjectId { get; set; }

        [JsonProperty("flow_timeobj"), JsonPropertyName("flow_timeobj")]
        public FlowTimeObject? FlowTimeObject { get; set; }

        [JsonProperty("flow_active"), JsonPropertyName("flow_active")]
        public bool FlowActive { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed { get; set; }
    }
}
