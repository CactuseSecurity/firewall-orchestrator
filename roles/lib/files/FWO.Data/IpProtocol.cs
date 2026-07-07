using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class IpProtocol
    {
        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int Id { get; set; }

        [JsonProperty("ip_proto_name"), JsonPropertyName("ip_proto_name")]
        public string Name { get; set; } = "";

        public bool HasPorts()
        {
            return HasPorts(Id);
        }

        public static bool HasPorts(int? id)
        {
            return id == 6 || id == 17;
        }
    }
}
