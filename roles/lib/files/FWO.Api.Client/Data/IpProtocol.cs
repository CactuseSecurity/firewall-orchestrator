using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class IpProtocol
    {
        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int Id { get; set; }

        [JsonProperty("ip_proto_name"), JsonPropertyName("ip_proto_name")]
        public string Name { get; set; } = "";
    }
}
