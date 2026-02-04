using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedServiceObject
    {
        [JsonProperty("svc_uid"), JsonPropertyName("svc_uid")]
        public string SvcUid { get; set; } = "";

        [JsonProperty("svc_name"), JsonPropertyName("svc_name")]
        public string SvcName { get; set; } = "";

        [JsonProperty("svc_port"), JsonPropertyName("svc_port")]
        public int? SvcPort { get; set; }

        [JsonProperty("svc_port_end"), JsonPropertyName("svc_port_end")]
        public int? SvcPortEnd { get; set; }

        [JsonProperty("svc_color"), JsonPropertyName("svc_color")]
        public string SvcColor { get; set; } = "";

        [JsonProperty("svc_typ"), JsonPropertyName("svc_typ")]
        public string SvcType { get; set; } = "";

        [JsonProperty("ip_proto"), JsonPropertyName("ip_proto")]
        public int? IpProtocol { get; set; }

        [JsonProperty("svc_member_refs"), JsonPropertyName("svc_member_refs")]
        public string? SvcMemberRefs { get; set; }

        [JsonProperty("svc_member_names"), JsonPropertyName("svc_member_names")]
        public string? SvcMemberNames { get; set; }

        [JsonProperty("svc_comment"), JsonPropertyName("svc_comment")]
        public string? SvcComment { get; set; }

        [JsonProperty("svc_timeout"), JsonPropertyName("svc_timeout")]
        public int? SvcTimeout { get; set; }

        [JsonProperty("rpc_nr"), JsonPropertyName("rpc_nr")]
        public string? RpcNumber { get; set; }

        public static NormalizedServiceObject FromNetworkService(NetworkService networkService)
        {
            return new NormalizedServiceObject
            {
                SvcUid = networkService.Uid,
                SvcName = networkService.Name,
                SvcPort = networkService.DestinationPort,
                SvcPortEnd = networkService.DestinationPortEnd,
                SvcColor = networkService.Color?.Name ?? "",
                SvcType = networkService.Type.Name,
                IpProtocol = networkService.ProtoId,
                SvcMemberRefs = networkService.MemberRefs,
                SvcMemberNames = networkService.MemberNames,
                SvcComment = networkService.Comment,
                SvcTimeout = networkService.Timeout,
                RpcNumber = networkService.RpcNumber?.ToString()
            };
        }
    }
}
