using System.Text.Json.Serialization;
// using Newtonsoft.Json;


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
        // [JsonProperty("areas"), JsonPropertyName("areas")]
        [JsonPropertyName("areas")]
        public List<ModellingImportAreaData> Areas { get; set; } = [];
    }

    /// <summary>
    /// Structure for imported area data 
    /// </summary>
    public class ModellingImportAreaData
    {
        /// <summary>
        /// Area Name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Area Id String
        /// </summary>
        [JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";

        /// <summary>
        /// List of all associated ip data
        /// </summary>
        [JsonPropertyName("subnets")]
        public List<ModellingImportAreaIpData> IpData { get; set; } = [];


        /// <summary>
        /// Overloaded constructor with an empty list as default
        /// </summary>
        public ModellingImportAreaData(string name, string idString)
            : this(name, idString, new List<ModellingImportAreaIpData>()) { }


        /// <summary>
        /// Constructor for initializing an object
        /// </summary>
        [JsonConstructor]
        public ModellingImportAreaData(string name, string idString, List<ModellingImportAreaIpData> ipData)
        {
            IdString = idString;
            Name = name;
            IpData = ipData;
        }
    }

    /// <summary>
    /// Structure for imported Area Ip Data 
    /// </summary>
    public class ModellingImportAreaIpData
    {
        /// <summary>
        /// Area Subnet Name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Area Subnet Network Start IP (in cidr notation)
        /// </summary>
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// Area Subnet Network End IP (in cidr notation)
        /// </summary>
        [JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; } = "";


        /// <summary>
        /// Clone an IP Object
        /// </summary>
        public ModellingImportAreaIpData Clone()
        {
            return new ModellingImportAreaIpData
            {
                Name = this.Name,
                Ip = this.Ip,
                IpEnd = this.IpEnd
            };
        }
    }
}
