using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Net;

namespace FWO.Api.Data
{
    public class NetworkSubnet
    {
        public IPAddress Address { get; set; }

        public IPAddress Mask { get; set; }
    }

    public class NetworkArea
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        public List<NetworkSubnet> Subnets { get; set; }
    }
}
