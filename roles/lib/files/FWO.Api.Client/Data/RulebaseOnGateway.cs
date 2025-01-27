using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RulebaseLink
    {
        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        [JsonProperty("from_rule_id"), JsonPropertyName("from_rule_id")]
        public int RulebaseId { get; set; }

        [JsonProperty("link_type"), JsonPropertyName("link_type")]
        public int LinkType { get; set; }

        [JsonProperty("rulebase"), JsonPropertyName("rulebase")]
        public Rulebase Rulebase { get; set; } = new Rulebase();

    }
}
