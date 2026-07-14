using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RuleTime
    {
        [JsonProperty("rule_time_id"), JsonPropertyName("rule_time_id")]
        public long Id { get; set; }

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        [JsonProperty("time_obj_id"), JsonPropertyName("time_obj_id")]
        public long TimeObjId { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed { get; set; }

        [JsonProperty("time_object"), JsonPropertyName("time_object")]
        public TimeObject? TimeObj { get; set; }
    }
}
