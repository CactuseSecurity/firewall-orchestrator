using System.Text.Json.Serialization;

namespace FWO.Config.Api.Data
{
    public class ConfigItem
    {
        [JsonPropertyName("config_key")]
        public string Key { get; set; }

        [JsonPropertyName("config_value")]
        public string Value { get; set; }

        [JsonPropertyName("config_user")]
        public int User { get; set; }
    }
}
