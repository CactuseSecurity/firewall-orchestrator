using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RuleRuleTime
    {
        [JsonProperty("rule_rule_time_id"), JsonPropertyName("rule_rule_time_id")]
        public long Id { get; set; }

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        [JsonProperty("rule_time_id"), JsonPropertyName("rule_time_id")]
        public long RuleTimeId { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public int Removed { get; set; } = 0;
    }
}
