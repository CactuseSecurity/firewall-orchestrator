using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Config.Api.Data
{
    public class ConfigItem
    {
        [JsonProperty("config_key"), JsonPropertyName("config_key")]
        public string Key { get; set; } = "";

        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public string? Value { get; set; }

        [JsonProperty("config_user"), JsonPropertyName("config_user")]
        public int User { get; set; }
    }
}
