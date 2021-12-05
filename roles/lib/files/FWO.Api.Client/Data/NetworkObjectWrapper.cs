using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkObjectWrapper
    {
        [JsonPropertyName("object")]
        public NetworkObject Content { get; set; } = new NetworkObject(){};
    }
}
