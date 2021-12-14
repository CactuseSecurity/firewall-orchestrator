using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RuleMetadata
    {
        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long Id { get; set; }

        [JsonProperty("rule_created"), JsonPropertyName("rule_created")]
        public DateTime? Created { get; set; }

        [JsonProperty("rule_last_modified"), JsonPropertyName("rule_last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonProperty("rule_first_hit"), JsonPropertyName("rule_first_hit")]
        public DateTime? FirstHit { get; set; }

        [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
        public DateTime? LastHit { get; set; }

        [JsonProperty("rule_last_certified"), JsonPropertyName("rule_last_certified")]
        public DateTime? LastCertified { get; set; }

        [JsonProperty("rule_last_certifier_dn"), JsonPropertyName("rule_last_certifier_dn")]
        public string LastCertifierDn { get; set; } = "";

        [JsonProperty("rule_to_be_removed"), JsonPropertyName("rule_to_be_removed")]
        public bool ToBeRemoved { get; set; }

        [JsonProperty("rule_decert_date"), JsonPropertyName("rule_decert_date")]
        public DateTime? DecertificationDate { get; set; }

        [JsonProperty("rule_recertification_comment"), JsonPropertyName("rule_recertification_comment")]
        public string Comment { get; set; } = "";

        public DateTime NextRecert { get; set; }

        public string LastCertifierName { get; set; } = "";

        public bool Recert { get; set; } 

        public string Style { get; set; } = "";
    }
}
