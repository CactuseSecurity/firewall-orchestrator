using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Active assignment of a label to the source or destination of a connection (labelling.connection_source_label /
    /// connection_destination_label). The labels of an AND-group are ANDed, the groups of a side are ORed.
    /// </summary>
    public class ConnectionLabel
    {
        /// <summary>
        /// Id of the assigned label.
        /// </summary>
        [JsonProperty("label_id"), JsonPropertyName("label_id")]
        public long LabelId { get; set; }

        /// <summary>
        /// Number of the AND-group of the side the label belongs to.
        /// </summary>
        [JsonProperty("and_group"), JsonPropertyName("and_group")]
        public int AndGroup { get; set; }

        /// <summary>
        /// The assigned label with its key.
        /// </summary>
        [JsonProperty("label_value"), JsonPropertyName("label_value")]
        public LabelValue Label { get; set; } = new();

        /// <summary>
        /// The label as "key: value".
        /// </summary>
        public string Display()
        {
            return $"{Label.Key.Name}: {Label.Value}";
        }
    }
}
