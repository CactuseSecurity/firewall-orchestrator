using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkZone
    {
        [JsonProperty("zone_id"), JsonPropertyName("zone_id")]
        public int Id { get; set; }

        [JsonProperty("zone_name"), JsonPropertyName("zone_name")]
        public string Name { get; set; } = "";
    }
}
