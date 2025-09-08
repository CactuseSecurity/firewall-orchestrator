
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class LinkType
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}