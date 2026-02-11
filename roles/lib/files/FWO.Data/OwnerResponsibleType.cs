using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class OwnerResponsibleType
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("allow_modelling"), JsonPropertyName("allow_modelling")]
        public bool AllowModelling { get; set; }

        [JsonProperty("allow_recertification"), JsonPropertyName("allow_recertification")]
        public bool AllowRecertification { get; set; }

        [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }
    }
}
