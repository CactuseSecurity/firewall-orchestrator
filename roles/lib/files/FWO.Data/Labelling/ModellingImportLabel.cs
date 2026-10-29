using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Structure for an imported key/value label
    /// </summary>
    public class ModellingImportLabel
    {
        /// <summary>
        /// Label key, e.g. stage
        /// </summary>
        [JsonProperty("label_key"), JsonPropertyName("label_key")]
        public string Key { get; set; } = "";

        /// <summary>
        /// Label value, e.g. prod
        /// </summary>
        [JsonProperty("label_value"), JsonPropertyName("label_value")]
        public string Value { get; set; } = "";
    }
}
