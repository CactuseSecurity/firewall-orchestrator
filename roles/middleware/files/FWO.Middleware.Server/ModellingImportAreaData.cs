using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Middleware.Server
{
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
        /// List of all associated Subnets
        /// </summary>
        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<ModellingImportAreaSubnets> Subnets { get; set; } = new();
    }
    
    /// <summary>
    /// Structure for imported Area Subnets 
    /// </summary>
    public class ModellingImportAreaSubnets
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
