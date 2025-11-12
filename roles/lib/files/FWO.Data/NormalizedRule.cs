using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedRule
    {
        [JsonProperty("rule_num"), JsonPropertyName("rule_num")]
        public int RuleNum { get; set; }

        [JsonProperty("rule_num_numeric"), JsonPropertyName("rule_num_numeric")]
        public double RuleNumNumeric { get; set; }

        [JsonProperty("rule_disabled"), JsonPropertyName("rule_disabled")]
        public bool RuleDisabled { get; set; }

        [JsonProperty("rule_src_neg"), JsonPropertyName("rule_src_neg")]
        public bool RuleSrcNeg { get; set; }

        [JsonProperty("rule_src"), JsonPropertyName("rule_src")]
        public string RuleSrc { get; set; } = "";

        [JsonProperty("rule_src_refs"), JsonPropertyName("rule_src_refs")]
        public string RuleSrcRefs { get; set; } = "";

        [JsonProperty("rule_dst_neg"), JsonPropertyName("rule_dst_neg")]
        public bool RuleDstNeg { get; set; }

        [JsonProperty("rule_dst"), JsonPropertyName("rule_dst")]
        public string RuleDst { get; set; } = "";

        [JsonProperty("rule_dst_refs"), JsonPropertyName("rule_dst_refs")]
        public string RuleDstRefs { get; set; } = "";

        [JsonProperty("rule_svc_neg"), JsonPropertyName("rule_svc_neg")]
        public bool RuleSvcNeg { get; set; }

        [JsonProperty("rule_svc"), JsonPropertyName("rule_svc")]
        public string RuleSvc { get; set; } = "";

        [JsonProperty("rule_svc_refs"), JsonPropertyName("rule_svc_refs")]
        public string RuleSvcRefs { get; set; } = "";

        [JsonProperty("rule_action"), JsonPropertyName("rule_action")]
        public string RuleAction { get; set; } = "";

        [JsonProperty("rule_track"), JsonPropertyName("rule_track")]
        public string RuleTrack { get; set; } = "";

        [JsonProperty("rule_installon"), JsonPropertyName("rule_installon")]
        public string? RuleInstallOn { get; set; }

        [JsonProperty("rule_time"), JsonPropertyName("rule_time")]
        public string? RuleTime { get; set; }

        [JsonProperty("rule_name"), JsonPropertyName("rule_name")]
        public string? RuleName { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? RuleUid { get; set; }

        [JsonProperty("rule_custom_fields"), JsonPropertyName("rule_custom_fields")]
        public string? RuleCustomFields { get; set; }

        [JsonProperty("rule_implied"), JsonPropertyName("rule_implied")]
        public bool RuleImplied { get; set; }

        [JsonProperty("rule_type"), JsonPropertyName("rule_type")]
        public string RuleType { get; set; } = "";

        [JsonProperty("rule_last_change_admin"), JsonPropertyName("rule_last_change_admin")]
        public string? RuleLastChangeAdmin { get; set; }

        [JsonProperty("parent_rule_uid"), JsonPropertyName("parent_rule_uid")]
        public string? ParentRuleUid { get; set; }

        [JsonProperty("last_hit"), JsonPropertyName("last_hit")]
        public string? LastHit { get; set; }

        [JsonProperty("rule_comment"), JsonPropertyName("rule_comment")]
        public string? RuleComment { get; set; }

        [JsonProperty("rule_src_zone"), JsonPropertyName("rule_src_zone")]
        public string? RuleSrcZone { get; set; }

        [JsonProperty("rule_dst_zone"), JsonPropertyName("rule_dst_zone")]
        public string? RuleDstZone { get; set; }

        [JsonProperty("rule_head_text"), JsonPropertyName("rule_head_text")]
        public string? RuleHeadText { get; set; }

        /// <summary>
        /// Creates a NormalizedRule from a Rule.
        /// </summary>
        /// <param name="rule">The Rule to normalize.</param>
        /// <returns>A normalized Rule.</returns>
        public static NormalizedRule FromRule(Rule rule)
        {
            DateTime? lastHit = rule.Metadata.LastHit;
            string? lastHitFormatted = null;
            if (lastHit.HasValue)
            {
                lastHitFormatted = lastHit.Value.ToString("yyyy-MM-ddTHH:mm") + lastHit.Value.ToString("zzz").Replace(":", "");
            }

            return new NormalizedRule
            {
                RuleNum = rule.RuleOrderNumber,
                RuleNumNumeric = rule.OrderNumber,
                RuleDisabled = rule.Disabled,
                RuleSrcNeg = rule.SourceNegated,
                RuleSrc = rule.Source,
                RuleSrcRefs = rule.SourceRefs,
                RuleDstNeg = rule.DestinationNegated,
                RuleDst = rule.Destination,
                RuleDstRefs = rule.DestinationRefs,
                RuleSvcNeg = rule.ServiceNegated,
                RuleSvc = rule.Service,
                RuleSvcRefs = rule.ServiceRefs,
                RuleAction = rule.Action,
                RuleTrack = rule.Track,
                RuleInstallOn = rule.InstallOn,
                RuleTime = rule.Time,
                RuleName = rule.Name,
                RuleUid = rule.Uid,
                RuleCustomFields = rule.CustomFields,
                RuleImplied = rule.Implied,
                RuleType = rule.NatRule ? "nat" : "access",
                RuleLastChangeAdmin = rule.LastChangeAdmin?.Name,
                ParentRuleUid = rule.ParentRule?.Uid,
                LastHit = lastHitFormatted,
                RuleComment = rule.Comment,
                RuleSrcZone = rule.RuleFromZones?.Length > 0 ? string.Join("|", rule.RuleFromZones.Select(z => z.Content.Name)) : null,
                RuleDstZone = rule.RuleToZones?.Length > 0 ? string.Join("|", rule.RuleToZones.Select(z => z.Content.Name)) : null,
                RuleHeadText = rule.SectionHeader
            };
        }       
    }
}
