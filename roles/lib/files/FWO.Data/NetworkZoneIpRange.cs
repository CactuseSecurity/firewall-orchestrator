using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    /// <summary>
    /// One row of network_zone.ip_range as the API reports it.
    /// </summary>
    public class NetworkZoneIpRange
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("network_zone_id"), JsonPropertyName("network_zone_id")]
        public int NetworkZoneId { get; set; }

        /// <summary>
        /// First address of the range, may come in CIDR notation ("10.0.0.1/32"); parse with
        /// IPAddressRange.Parse(...).Begin, which accepts both spellings and IPv6.
        /// </summary>
        [JsonProperty("ip_range_start"), JsonPropertyName("ip_range_start")]
        public string IpRangeStart { get; set; } = "";

        /// <summary>
        /// Last address of the range. See caveat in <see cref="IpRangeStart"/> documentation.
        /// </summary>
        [JsonProperty("ip_range_end"), JsonPropertyName("ip_range_end")]
        public string IpRangeEnd { get; set; } = "";
    }
}
