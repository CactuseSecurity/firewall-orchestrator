using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Config.Api.Data
{
    /// <summary>
    /// contains all texts needed for displaying UI in different languages
    /// </summary>
    public class UiText
    {
        [JsonProperty("txt"), JsonPropertyName("txt")]
        public string Txt { get; set; } = "";

        [JsonProperty("id"), JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonProperty("language"), JsonPropertyName("language")]
        public string Language { get; set; } = "";
    }
}
