using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class PathAnalysisAlgorithm
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
