using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RuleTime
    {
        [JsonProperty("rule_time_id"), JsonPropertyName("rule_time_id")]
        public long Id { get; set; }

        [JsonProperty("time_type"), JsonPropertyName("time_type")]
        public long TimeType { get; set; } // timespan, schedule, etc.

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty("end_date"), JsonPropertyName("end_date")]
        public DateTime EndTime { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public int Removed { get; set; } = 0;

    }
}