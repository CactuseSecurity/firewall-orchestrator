using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ServiceWrapper
    {
        [JsonPropertyName("service")]
        public NetworkService Content { get; set; } = new NetworkService();
    }
    // public class ServiceObjectRecursiveWrapper
    // {
    //     [JsonPropertyName("service")]
    //     public ServiceObjectRecursiveFlatsWrapper Content { get; set; }
    // }
}
