using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class Recertification
    {

        [JsonProperty("recert_date"), JsonPropertyName("recert_date")]
        public DateTime? RecertDate { get; set; }

        [JsonProperty("recertified"), JsonPropertyName("recertified")]
        public bool Recertified { get; set; } = false;

        [JsonProperty("ip_match"), JsonPropertyName("ip_match")]
        public string IpMatch { get; set; } = "";

        [JsonProperty("next_recert_date"), JsonPropertyName("next_recert_date")]
        public DateTime? NextRecertDate { get; set; }

        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner? FwoOwner { get; set; } = new FwoOwner();

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public FwoOwner? Comment { get; set; } = new FwoOwner();
    }

}
