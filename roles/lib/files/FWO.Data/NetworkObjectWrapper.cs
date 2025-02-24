using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkObjectWrapper
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Content { get; set; } = new NetworkObject(){};
    }
}
