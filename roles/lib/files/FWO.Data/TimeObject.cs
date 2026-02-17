using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class TimeObject
    {
        [JsonProperty("time_obj_id"), JsonPropertyName("time_obj_id")]
        public long Id { get; set; }

        [JsonProperty("time_obj_type"), JsonPropertyName("time_obj_type")]
        public long TimeType { get; set; } // timespan, schedule, etc.

        [JsonProperty("time_obj_name"), JsonPropertyName("time_obj_name")]
        public string Name { get; set; } = "";

        [JsonProperty("time_obj_uid"), JsonPropertyName("time_obj_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty("end_time"), JsonPropertyName("end_time")]
        public DateTime EndTime { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created { get; set; }
    }
}