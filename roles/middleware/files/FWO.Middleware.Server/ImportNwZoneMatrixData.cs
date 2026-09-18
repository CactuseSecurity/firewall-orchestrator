using System.Text.Json.Serialization;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Structure for imported Network Zones matrix data
    /// </summary>
    public class ImportNwZoneMatrixData
    {
        /// <summary>
        /// Matrix Name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Matrix Comment
        /// </summary>
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "";

        /// <summary>
        /// List of all Network Zones
        /// </summary>
        [JsonPropertyName("areas")]
        public List<NetworkZoneData> NetworkZones { get; set; } = [];
    }

    /// <summary>
    /// Structure for imported Network Zones data
    /// </summary>
    public class NetworkZoneData
    {
        /// <summary>
        /// Zone Name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Stable technical zone identifier
        /// </summary>
        [JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";

        /// <summary>
        /// List of all ip ranges assigned to the zone
        /// </summary>
        [JsonPropertyName("subnets")]
        public List<ZoneIpRangeData> IpData { get; set; } = [];

        /// <summary>
        /// List of all associated communication data
        /// </summary>
        [JsonPropertyName("communication_to")]
        public List<CommunicationData> CommData { get; set; } = [];
    }

    /// <summary>
    /// Structure for an imported ip range of a network zone, including its device paths
    /// </summary>
    public class ZoneIpRangeData
    {
        /// <summary>
        /// Optional descriptive name of the ip range
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Single ip address, cidr network, explicit range, or start of a range
        /// </summary>
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// Last address of an inclusive range
        /// </summary>
        [JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; } = "";

        /// <summary>
        /// Ordered path of existing devices from this ip range towards the root.
        /// Missing or empty means the ip range has no root path.
        /// </summary>
        [JsonPropertyName("path_to_root")]
        public List<DeviceRefData> PathToRoot { get; set; } = [];

        /// <summary>
        /// Ordered path of existing devices from this ip range towards the internet.
        /// Missing or empty means the ip range has no internet path.
        /// </summary>
        [JsonPropertyName("path_to_internet")]
        public List<DeviceRefData> PathToInternet { get; set; } = [];
    }

    /// <summary>
    /// Reference to an existing device by its management and device name
    /// </summary>
    public class DeviceRefData
    {
        /// <summary>
        /// Name of the management the device belongs to
        /// </summary>
        [JsonPropertyName("mgmt_name")]
        public string MgmtName { get; set; } = "";

        /// <summary>
        /// Name of the device
        /// </summary>
        [JsonPropertyName("device_name")]
        public string DeviceName { get; set; } = "";
    }

    /// <summary>
    /// Structure for communication Data
    /// </summary>
    public class CommunicationData
    {
        /// <summary>
        /// Reference to other Network Zone as destination
        /// </summary>
        [JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";
    }
}
