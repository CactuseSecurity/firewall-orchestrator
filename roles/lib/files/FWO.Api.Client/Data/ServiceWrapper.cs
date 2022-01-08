using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ServiceWrapper
    {
        [JsonProperty("service"), JsonPropertyName("service")]
        public NetworkService Content { get; set; } = new NetworkService();
    }
    // public class ServiceObjectRecursiveWrapper
    // {
    //     [JsonProperty("service"), JsonPropertyName("service")]
    //     public ServiceObjectRecursiveFlatsWrapper Content { get; set; }
    // }
}
