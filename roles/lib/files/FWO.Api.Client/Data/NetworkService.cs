using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkService
    {
        [JsonProperty("svc_id"), JsonPropertyName("svc_id")]
        public long Id { get; set; }

        [JsonProperty("svc_name"), JsonPropertyName("svc_name")]
        public string Name { get; set; } = "";

        [JsonProperty("svc_uid"), JsonPropertyName("svc_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("svc_port"), JsonPropertyName("svc_port")]
        public int? DestinationPort { get; set; }

        [JsonProperty("svc_port_end"), JsonPropertyName("svc_port_end")]
        public int? DestinationPortEnd { get; set; }

        [JsonProperty("svc_source_port"), JsonPropertyName("svc_source_port")]
        public int? SourcePort { get; set; }

        [JsonProperty("svc_source_port_end"), JsonPropertyName("svc_source_port_end")]
        public int? SourcePortEnd { get; set; }

        [JsonProperty("svc_code"), JsonPropertyName("svc_code")]
        public string Code { get; set; } = "";

        [JsonProperty("svc_timeout"), JsonPropertyName("svc_timeout")]
        public int? Timeout { get; set; }

        [JsonProperty("svc_typ_id"), JsonPropertyName("svc_typ_id")]
        public int? TypeId { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonProperty("svc_create"), JsonPropertyName("svc_create")]
        public int Create { get; set; }

        [JsonProperty("svc_create_time"), JsonPropertyName("svc_create_time")]
        public TimeWrapper CreateTime { get; set; } = new();

        [JsonProperty("svc_last_seen"), JsonPropertyName("svc_last_seen")]
        public int LastSeen { get; set; }

        [JsonProperty("service_type"), JsonPropertyName("service_type")]
        public NetworkServiceType Type { get; set; } = new();

        [JsonProperty("svc_comment"), JsonPropertyName("svc_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("svc_color_id"), JsonPropertyName("svc_color_id")]
        public int? ColorId { get; set; }

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int? ProtoId { get; set; }

        [JsonProperty("protocol_name"), JsonPropertyName("protocol_name")]
        public NetworkProtocol Protocol { get; set; } = new();

        [JsonProperty("svc_member_names"), JsonPropertyName("svc_member_names")]
        public string MemberNames { get; set; } = "";

        [JsonProperty("svc_member_refs"), JsonPropertyName("svc_member_refs")]
        public string MemberRefs { get; set; } = "";

        [JsonProperty("svcgrps"), JsonPropertyName("svcgrps")]
        public Group<NetworkService>[] ServiceGroups { get; set; } = [];

        [JsonProperty("svcgrp_flats"), JsonPropertyName("svcgrp_flats")]
        public GroupFlat<NetworkService>[] ServiceGroupFlats { get; set; } = [];

        public long Number;

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                NetworkService nsrv => Id == nsrv.Id,
                _ => base.Equals(obj),
            };
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public string MemberNamesAsHtml()
        {
            if (MemberNames != null && MemberNames.Contains("|"))
            {
                return $"<td>{string.Join("<br>", MemberNames.Split('|'))}</td>";
            }
            else
            {
                return $"<td>{MemberNames}</td>";
            }
        }
    }
}
