using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedRulebaseLink
    {
        [JsonProperty("from_rulebase_uid"), JsonPropertyName("from_rulebase_uid")]
        public string? FromRulebaseUid { get; set; }

        [JsonProperty("from_rule_uid"), JsonPropertyName("from_rule_uid")]
        public string? FromRuleUid { get; set; }

        [JsonProperty("to_rulebase_uid"), JsonPropertyName("to_rulebase_uid")]
        public string ToRulebaseUid { get; set; } = "";

        [JsonProperty("link_type"), JsonPropertyName("link_type")]
        public string LinkType { get; set; } = "";

        [JsonProperty("is_initial"), JsonPropertyName("is_initial")]
        public bool IsInitial { get; set; }

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; }

        [JsonProperty("is_section"), JsonPropertyName("is_section")]
        public bool IsSection { get; set; }

        public static NormalizedRulebaseLink FromRulebaseLink(RulebaseLink rulebaseLink)
        {
            return new NormalizedRulebaseLink
            {
                FromRulebaseUid = rulebaseLink.FromRulebase?.Uid,
                FromRuleUid = rulebaseLink.FromRule?.Uid,
                ToRulebaseUid = rulebaseLink.ToRulebase?.Uid ?? "",
                LinkType = rulebaseLink.LinkTypeObj?.Name ?? "",
                IsInitial = rulebaseLink.IsInitial,
                IsGlobal = rulebaseLink.IsGlobal,
                IsSection = rulebaseLink.IsSection
            };
        }
    }
}
