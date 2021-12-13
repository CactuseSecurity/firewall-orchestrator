using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Config.Api.Data
{
    /// <summary>
    /// a list of all available languages
    /// </summary>
    public class Language
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("culture_info"), JsonPropertyName("culture_info")]
        public string CultureInfo { get; set; } = "";

        // might later also add the full culture name, if needed:
        // [JsonProperty("culture_name_english"), JsonPropertyName("culture_name_english")]
        // public string CultureNameEnglish { get; set; }

    }
}
