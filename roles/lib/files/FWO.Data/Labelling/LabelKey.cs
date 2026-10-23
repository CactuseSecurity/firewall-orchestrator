using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Key of key/value labels (labelling.label_key), e.g. Stage.
    /// </summary>
    public class LabelKey
    {
        /// <summary>
        /// Id of the key.
        /// </summary>
        [JsonProperty("key_id"), JsonPropertyName("key_id")]
        public int Id { get; set; }

        /// <summary>
        /// Unique name of the key.
        /// </summary>
        [JsonProperty("key_name"), JsonPropertyName("key_name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// True if the key is maintained within FWO, false if it is imported.
        /// </summary>
        [JsonProperty("key_source_internal"), JsonPropertyName("key_source_internal")]
        public bool SourceInternal { get; set; }

        /// <summary>
        /// True if the values of the key belong to an owner.
        /// </summary>
        [JsonProperty("key_is_owner_scoped"), JsonPropertyName("key_is_owner_scoped")]
        public bool OwnerScoped { get; set; }

        /// <summary>
        /// True if an object may carry several values of the key.
        /// </summary>
        [JsonProperty("allow_multiple_values"), JsonPropertyName("allow_multiple_values")]
        public bool AllowMultipleValues { get; set; }
    }
}
