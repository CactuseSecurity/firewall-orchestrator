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
        /// Area Subnet Network (in cidr notation)
        /// </summary>
        [JsonProperty("network"), JsonPropertyName("network")]
        public string Network { get; set; } = "";
    }
}
