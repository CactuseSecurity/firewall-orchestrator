using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkObjectType
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
