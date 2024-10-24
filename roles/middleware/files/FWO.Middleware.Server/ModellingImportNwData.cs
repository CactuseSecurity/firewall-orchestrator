using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Structure for imported network data 
    /// </summary>
    public class ModellingImportNwData
    {
        /// <summary>
        /// List of all Areas
        /// </summary>
        [JsonProperty("areas"), JsonPropertyName("areas")]
        public List<ModellingImportAreaData>? Areas { get; set; }= [];
    }

    /// <summary>
    /// Structure for imported area data 
    /// </summary>
    public class ModellingImportAreaData
    {
        /// <summary>
        /// Area Name
        /// </summary>
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Area Id String
        /// </summary>
        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";

        /// <summary>
        /// List of all associated ip data
        /// </summary>
        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<ModellingImportAreaIpData> IpData { get; set; } = [];
    }
    
    /// <summary>
    /// Structure for imported Area Ip Data 
    /// </summary>
    public class ModellingImportAreaIpData
    {
        /// <summary>
        /// Area Subnet Name
        /// </summary>
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Area Subnet Network Start IP (in cidr notation)
        /// </summary>
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// Area Subnet Network End IP (in cidr notation)
        /// </summary>
        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; } = "";

    }
}
