using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Assignment of a label to an owner network, e.g. an app server (labelling.owner_network_label).
    /// </summary>
    public class OwnerNetworkLabel
    {
        /// <summary>
        /// Id of the owner network.
        /// </summary>
        [JsonProperty("owner_network_id"), JsonPropertyName("owner_network_id")]
        public long OwnerNetworkId { get; set; }

        /// <summary>
        /// Id of the assigned label.
        /// </summary>
        [JsonProperty("label_id"), JsonPropertyName("label_id")]
        public long LabelId { get; set; }
    }
}
