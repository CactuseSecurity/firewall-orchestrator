using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkLocation
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; } = new NetworkObject(){};

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser User { get; set; } = new NetworkUser(){};
    }
}
