using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkLocation
    {
        [JsonPropertyName("object")]
        public NetworkObject Object { get; set; } = new NetworkObject(){};

        [JsonPropertyName("usr")]
        public NetworkUser User { get; set; } = new NetworkUser(){};
    }
}
