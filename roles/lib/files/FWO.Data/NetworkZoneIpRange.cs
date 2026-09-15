using System.Net;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkZoneIpRange
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("network_zone_id"), JsonPropertyName("network_zone_id")]
        public int NetworkZoneId { get; set; }
        [JsonProperty("ip_range_start"), JsonPropertyName("ip_range_start")]
        public string IpRangeStart { get; set; } = "";
        [JsonProperty("ip_range_end"), JsonPropertyName("ip_range_end")]
        public string IpRangeEnd { get; set; } = "";
    }
}
