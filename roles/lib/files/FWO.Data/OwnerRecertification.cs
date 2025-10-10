using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class OwnerRecertification
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonProperty("user_dn"), JsonPropertyName("user_dn")]
        public string? RecertifierDn { get; set; }

        [JsonProperty("recert_date"), JsonPropertyName("recert_date")]
        public DateTime? RecertDate { get; set; }

        [JsonProperty("recertified"), JsonPropertyName("recertified")]
        public bool Recertified { get; set; } = false;

        [JsonProperty("next_recert_date"), JsonPropertyName("next_recert_date")]
        public DateTime? NextRecertDate { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("report_id"), JsonPropertyName("report_id")]
        public long? ReportId { get; set; }
    }
}
