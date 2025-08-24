using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedConfig
    {
        [JsonProperty("ConfigFormat"), JsonPropertyName("ConfigFormat")]
        public string ConfigFormat { get; set; } = "";

        [JsonProperty("action"), JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [JsonProperty("network_objects"), JsonPropertyName("network_objects")]
        public Dictionary<string, NormalizedNetworkObject> NetworkObjects { get; set; } = [];

        [JsonProperty("service_objects"), JsonPropertyName("service_objects")]
        public Dictionary<string, NormalizedServiceObject> ServiceObjects { get; set; } = [];

        [JsonProperty("users"), JsonPropertyName("users")]
        public Dictionary<string, object> Users { get; set; } = [];

        [JsonProperty("zone_objects"), JsonPropertyName("zone_objects")]
        public Dictionary<string, NormalizedZoneObject> ZoneObjects { get; set; } = [];

        [JsonProperty("rulebases"), JsonPropertyName("rulebases")]
        public NormalizedRulebase[] Rulebases { get; set; } = [];

        [JsonProperty("gateways"), JsonPropertyName("gateways")]
        public NormalizedGateway[] Gateways { get; set; } = [];
    }
}