using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowNwObject
    {
        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("ip_start"), JsonPropertyName("ip_start")]
        public string? IpStart { get; set; }

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; }

        [JsonProperty("nwobj_hash"), JsonPropertyName("nwobj_hash")]
        public string Hash { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        public void GenerateHash()
        {
            Hash = FlowHashGenerator.GenerateNwObjectHash(IpStart, IpEnd);
        }
    }
}
