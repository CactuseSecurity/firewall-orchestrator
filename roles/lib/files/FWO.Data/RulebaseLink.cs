using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RulebaseLink
    {
        [JsonProperty("gw_id"), JsonPropertyName("gw_id")]
        public int GatewayId { get; set; }

        [JsonProperty("from_rule_id"), JsonPropertyName("from_rule_id")]
        public int? FromRuleId { get; set; }    // nullable for initial rulebase

        [JsonProperty("link_type"), JsonPropertyName("link_type")]
        public int LinkType { get; set; }

        [JsonProperty("to_rulebase_id"), JsonPropertyName("to_rulebase_id")]
        public int NextRulebaseId = new();

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created;

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed;

        public bool IsInitialRulebase()
        {
            return LinkType == 0;
        }

    }

}
