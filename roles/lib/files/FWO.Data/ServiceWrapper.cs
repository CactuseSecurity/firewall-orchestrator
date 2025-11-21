using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class ServiceWrapper
    {
        [JsonProperty("service"), JsonPropertyName("service")]
        public NetworkService Content { get; set; } = new NetworkService();
    }
}
