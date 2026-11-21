using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Value of a label key (labelling.label_value), e.g. Prod of the key Stage. Its id identifies the label.
    /// </summary>
    public class LabelValue
    {
        /// <summary>
        /// Id of the label.
        /// </summary>
        [JsonProperty("label_id"), JsonPropertyName("label_id")]
        public long Id { get; set; }

        /// <summary>
        /// Value of the label.
        /// </summary>
        [JsonProperty("value"), JsonPropertyName("value")]
        public string Value { get; set; } = "";

        /// <summary>
        /// Key of the label.
        /// </summary>
        [JsonProperty("label_key"), JsonPropertyName("label_key")]
        public LabelKey Key { get; set; } = new();
    }
}
