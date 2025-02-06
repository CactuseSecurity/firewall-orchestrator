using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RulebaseLink
    {
        [JsonProperty("gw_id"), JsonPropertyName("gw_id")]
        public int GatewayId { get; set; }

        [JsonProperty("from_rule_id"), JsonPropertyName("from_rule_id")]
        public int FromRuleId { get; set; }

        [JsonProperty("link_type"), JsonPropertyName("link_type")]
        public int LinkType { get; set; }

        [JsonProperty("to_rulebase_id"), JsonPropertyName("rulebase")]
        public int ToRulebaseId { get; set; }

        [JsonProperty("to_rulebase_id"), JsonPropertyName("rulebase")]
        public Rulebase NextRulebase = new();

    }
}
