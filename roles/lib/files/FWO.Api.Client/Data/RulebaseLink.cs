using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
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

        public bool IsInitialRulebase()
        {
            return LinkType == 0;
        }

    }

}
