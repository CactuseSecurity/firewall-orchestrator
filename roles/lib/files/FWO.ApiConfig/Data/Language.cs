using System.Text.Json.Serialization;

namespace FWO.ApiConfig.Data
{
    /// <summary>
    /// a list of all available languages
    /// </summary>
    public class Language
    {
        [JsonPropertyName("name")]
        public string Txt { get; set; }
    }
}
