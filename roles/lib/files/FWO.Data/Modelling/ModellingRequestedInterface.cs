using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Modelling
{
    /// <summary>
    /// Requested interface as listed in the requested interfaces overview: the connection list fields
    /// plus the ids of the active connections that use the interface.
    /// </summary>
    public class ModellingRequestedInterface : ModellingConnection
    {
        /// <summary>
        /// Active (not removed) connections that use this interface. Only the ids are filled.
        /// </summary>
        [JsonProperty("used_by_connections"), JsonPropertyName("used_by_connections")]
        public List<ModellingConnection> UsedByConnections { get; set; } = [];
    }
}
