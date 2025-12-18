using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RulebaseLink
    {
        [JsonProperty("gw_id"), JsonPropertyName("gw_id")]
        public int GatewayId { get; set; }

        [JsonProperty("from_rule_id"), JsonPropertyName("from_rule_id")]
        public int? FromRuleId { get; set; }    // nullable for initial rulebase and for fromRulebase links

        [JsonProperty("rule"), JsonPropertyName("rule")]
        public Rule? FromRule { get; set; }

        [JsonProperty("rulebaseByFromRulebaseId"), JsonPropertyName("rulebaseByFromRulebaseId")]
        public Rulebase? FromRulebase { get; set; }

        [JsonProperty("from_rulebase_id"), JsonPropertyName("from_rulebase_id")]
        public int? FromRulebaseId { get; set; }    // nullable for fromRule links

        [JsonProperty("rulebase"), JsonPropertyName("rulebase")]
        public Rulebase? ToRulebase { get; set; }

        [JsonProperty("link_type"), JsonPropertyName("link_type")]
        public int LinkType { get; set; }

        [JsonProperty("stm_link_type"), JsonPropertyName("stm_link_type")]
        public LinkType? LinkTypeObj { get; set; }

        [JsonProperty("is_initial"), JsonPropertyName("is_initial")]
        public bool IsInitial { get; set; } = false;

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; } = false;

        [JsonProperty("is_section"), JsonPropertyName("is_section")]
        public bool IsSection { get; set; } = false;

        [JsonProperty("to_rulebase_id"), JsonPropertyName("to_rulebase_id")]
        public int NextRulebaseId = new();

        [JsonProperty("created"), JsonPropertyName("created")]
        public long? Created;

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed;
    }
}
