using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FWO.ApiConfig.Data
{
    /// <summary>
    /// contains all texts needed for displaying UI in different languages
    /// </summary>
    public class UiText
    {
        [JsonPropertyName("txt")]
        public string Txt { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }
    }


    /// <summary>
    /// contains texts needed for displaying UI in a single language
    /// </summary>
    public class LocalizedText
    {
        private Dictionary<string,string> text;

        public LocalizedText()
        {

        }

    }
}
