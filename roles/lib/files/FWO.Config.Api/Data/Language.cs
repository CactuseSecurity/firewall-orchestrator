using System.Text.Json.Serialization;
namespace FWO.Config.Api.Data
{
    /// <summary>
    /// a list of all available languages
    /// </summary>
    public class Language
    {

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("culture_info")]
        public string CultureInfo { get; set; } = "";

        // might later also add the full culture name, if needed:
        // [JsonPropertyName("culture_name_english")]
        // public string CultureNameEnglish { get; set; }

    }
}
