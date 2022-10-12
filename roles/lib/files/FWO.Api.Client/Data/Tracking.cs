using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class Tracking
    {
        [JsonProperty("track_id"), JsonPropertyName("track_id")]
        public int Id { get; set; }

        [JsonProperty("track_name"), JsonPropertyName("track_name")]
        public string Name { get; set; } = "";
    }
}
