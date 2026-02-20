using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using NewtonsoftJsonIgnore = Newtonsoft.Json.JsonIgnoreAttribute;
using SystemTextJsonIgnore = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace FWO.Data
{
    public class RuleOwner
    {
        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int owner_id { get; set; }

        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long? rule_metadata_id { get; set; }

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long rule_id { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long created { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? removed { get; set; }

        [JsonProperty("owner_mapping_source_id"), JsonPropertyName("owner_mapping_source_id")]
        public long owner_mapping_source_id { get; set; }
    }


    public class RuleOwnerMutationWrapper
    {
        public UpdateRuleOwner? update_rule_owner { get; set; }

        public InsertRuleOwnerResult? insert_rule_owner { get; set; }
    }

    public class UpdateRuleOwner
    {
        public int affected_rows { get; set; }
    }

    public class InsertRuleOwnerResult
    {
        public int affected_rows { get; set; }
        public List<RuleOwner>? returning { get; set; }
    }

}
