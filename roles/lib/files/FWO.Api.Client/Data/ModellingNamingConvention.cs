using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNamingConvention
    {
        [JsonProperty("networkAreaRequired"), JsonPropertyName("networkAreaRequired")]
        public bool NetworkAreaRequired { get; set; } = false;

        [JsonProperty("useAppPart"), JsonPropertyName("useAppPart")]
        public bool UseAppPart { get; set; } = false;

        [JsonProperty("fixedPartLength"), JsonPropertyName("fixedPartLength")]
        public int FixedPartLength { get; set; }

        [JsonProperty("freePartLength"), JsonPropertyName("freePartLength")]
        public int FreePartLength { get; set; }

        [JsonProperty("networkAreaPattern"), JsonPropertyName("networkAreaPattern")]
        public string NetworkAreaPattern { get; set; } = "";

        [JsonProperty("appRolePattern"), JsonPropertyName("appRolePattern")]
        public string AppRolePattern { get; set; } = "";

        [JsonProperty("appServerPrefix"), JsonPropertyName("appServerPrefix")]
        public string? AppServerPrefix { get; set; } = "";
    }
}
