using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowSvcObject
    {
        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("port_start"), JsonPropertyName("port_start")]
        public int? PortStart { get; set; }

        [JsonProperty("port_end"), JsonPropertyName("port_end")]
        public int? PortEnd { get; set; }

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int ProtoId { get; set; }

        [JsonProperty("svcobj_hash"), JsonPropertyName("svcobj_hash")]
        public string Hash { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<NetworkService>? Services { get; set; }

        public void GenerateHash()
        {
            Hash = FlowHashGenerator.GenerateSvcObjectHash(ProtoId, PortStart, PortEnd);
        }
    }
}
