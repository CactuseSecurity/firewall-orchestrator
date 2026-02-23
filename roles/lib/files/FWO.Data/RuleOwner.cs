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
        public int OwnerId { get; set; }

        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long? RuleMetadataId { get; set; }

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed { get; set; }

        [JsonProperty("owner_mapping_source_id"), JsonPropertyName("owner_mapping_source_id")]
        public int OwnerMappingSourceId { get; set; }
    }


    public class RuleOwnerMutationWrapper
    {
        [JsonProperty("update_rule_owner"), JsonPropertyName("update_rule_owner")]
        public UpdateRuleOwner? UpdateRuleOwner { get; set; }

        [JsonProperty("insert_rule_owner"), JsonPropertyName("insert_rule_owner")]
        public InsertRuleOwnerResult? InsertRuleOwner { get; set; }
    }

    public class UpdateRuleOwner
    {
        [JsonProperty("affected_rows"), JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }
    }

    public class InsertRuleOwnerResult
    {
        [JsonProperty("affected_rows"), JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<RuleOwner>? Returning { get; set; }
    }

}
