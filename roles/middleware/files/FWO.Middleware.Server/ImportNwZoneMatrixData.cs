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
    public class NetworkZoneData : ModellingImportAreaData
    {
        /// <summary>
        /// List of all associated communication data
        /// </summary>
        [JsonPropertyName("communication_to")]
        public List<CommunicationData> CommData { get; set; } = [];
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
