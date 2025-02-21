using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RecertificationBase
    {

        [JsonProperty("recert_date"), JsonPropertyName("recert_date")]
        public DateTime? RecertDate { get; set; }

        [JsonProperty("recertified"), JsonPropertyName("recertified")]
        public bool Recertified { get; set; } = false;

        [JsonProperty("ip_match"), JsonPropertyName("ip_match")]
        public string IpMatch { get; set; } = "";

        [JsonProperty("next_recert_date"), JsonPropertyName("next_recert_date")]
        public DateTime? NextRecertDate { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long RuleMetadataId { get; set; }
    }

}
