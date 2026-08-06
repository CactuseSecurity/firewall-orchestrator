using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    /// <summary>
    /// Contains a flow object and the normalized objects that can supply its display name.
    /// </summary>
    public class FlowNamingCandidate
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("mappings"), JsonPropertyName("mappings")]
        public List<FlowNamingMapping> Mappings { get; set; } = [];
    }

    /// <summary>
    /// Identifies one normalized object that is mapped to a flow object.
    /// </summary>
    public class FlowNamingMapping
    {
        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int ManagementId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
