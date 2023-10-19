using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Net;
using NetTools;

namespace FWO.Api.Data
{
    public class NetworkSubnet
    {
        public string Name { get; set; }

        public string Address { get; set; }

        public string Mask { get; set; }

        // -> cidr
        public IPAddressRange IPAddressRange { get; set; }
    }

    public class NetworkArea
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        public List<NetworkSubnet> Subnets { get; set; }
    }
}
