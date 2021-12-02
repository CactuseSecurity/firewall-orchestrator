using System;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class RuleMetadata
    {
        [JsonPropertyName("rule_metadata_id")]
        public long Id { get; set; }

        [JsonPropertyName("rule_created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("rule_last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("rule_first_hit")]
        public DateTime? FirstHit { get; set; }

        [JsonPropertyName("rule_last_hit")]
        public DateTime? LastHit { get; set; }

        [JsonPropertyName("rule_last_certified")]
        public DateTime? LastCertified { get; set; }

        [JsonPropertyName("rule_last_certifier_dn")]
        public string LastCertifierDn { get; set; }

        [JsonPropertyName("rule_to_be_removed")]
        public bool ToBeRemoved { get; set; }

        [JsonPropertyName("rule_decert_date")]
        public DateTime? DecertificationDate { get; set; }

        [JsonPropertyName("rule_recertification_comment")]
        public string Comment { get; set; }


        public DateTime NextRecert { get; set; }

        public string LastCertifierName { get; set; }

        public bool Recert { get; set; } 

        public string Style { get; set; }
    }
}
