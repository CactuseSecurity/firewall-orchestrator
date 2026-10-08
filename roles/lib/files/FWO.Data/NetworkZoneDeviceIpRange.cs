using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkZoneDeviceIpRange
    {
        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        [JsonProperty("ip_range_id"), JsonPropertyName("ip_range_id")]
        public int IpRangeId { get; set; }
        /// <summary>
        /// Position of the device on the path from ip range to the root network, starting at 1.
        /// Position 1 is the gateway closest to the ip range, higher values lie further towards the root.
        /// Null when this row describes an internet path.
        /// </summary>
        [JsonProperty("order_to_root"), JsonPropertyName("order_to_root")]
        public int? OrderToRoot { get; set; }
        /// <summary>
        /// Position of the device on the path from ip range to the internet, starting at 1.
        /// Position 1 is the gateway closest to the ip range, higher values lie further towards internet.
        /// Null when this row describes a root path.
        /// </summary>
        [JsonProperty("order_to_internet"), JsonPropertyName("order_to_internet")]
        public int? OrderToInternet { get; set; }
        /// <summary>The device this path entry refers to, carrying its name.</summary>
        [JsonProperty("device"), JsonPropertyName("device")]
        public Device? Device { get; set; }
    }
}
