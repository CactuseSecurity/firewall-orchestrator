using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Net;
using NetTools;

namespace FWO.Api.Data
{
    public class NetworkSubnet
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // -> cidr
        [JsonProperty("network"), JsonPropertyName("network")]
        public string? Network { get; set; }


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Network = Sanitizer.SanitizeOpt(Network, ref shortened);
            return shortened;
        }

    }

    public class ModellingNetworkArea
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<NetworkSubnet> Subnets { get; set; }
    }
}
