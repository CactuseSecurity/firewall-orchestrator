using System.Text.Json.Serialization;
using System.Collections.Generic;
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


    /// <summary>
    /// contains texts needed for displaying UI in a single language
    /// </summary>
    public class SingleLanguage
    {
        public Dictionary<string,string> text { get; set; } = new Dictionary<string, string>();

        // key of all_text ref is a combination ${language,id}
        public SingleLanguage(string language, ref Dictionary<string,string> all_text)
        {
            
        }
    }
}
