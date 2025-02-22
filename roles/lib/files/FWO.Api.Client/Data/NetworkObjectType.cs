using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkObjectType
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = 0;
        
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
