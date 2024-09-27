using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ElemFieldType
    {
        source, 
        destination, 
        service,
        rule
    }

    public class WfElementBase
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string? IpString { get; set; }

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; }

        [JsonProperty("port"), JsonPropertyName("port")]
        public int? Port { get; set; }

        [JsonProperty("port_end"), JsonPropertyName("port_end")]
        public int? PortEnd { get; set; }

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int? ProtoId { get; set; }

        [JsonProperty("network_object_id"), JsonPropertyName("network_object_id")]
        public long? NetworkId { get; set; }

        [JsonProperty("service_id"), JsonPropertyName("service_id")]
        public long? ServiceId { get; set; }

        [JsonProperty("field"), JsonPropertyName("field")]
        public string Field { get; set; } = ElemFieldType.source.ToString();

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public long? UserId { get; set; }

        [JsonProperty("original_nat_id"), JsonPropertyName("original_nat_id")]
        public long? OriginalNatId { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? RuleUid { get; set; }

        [JsonProperty("group_name"), JsonPropertyName("group_name")]
        public string? GroupName { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }


        public WfElementBase()
        { }

        public WfElementBase(WfElementBase element)
        {
            IpString = element.IpString;
            IpEnd = element.IpEnd;
            Port = element.Port;
            PortEnd = element.PortEnd;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
            RuleUid = element.RuleUid;
            GroupName = element.GroupName;
            Name = element.Name;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            IpString = Sanitizer.SanitizeOpt(IpString, ref shortened);
            IpEnd = Sanitizer.SanitizeOpt(IpEnd, ref shortened);
            Field = Sanitizer.SanitizeMand(Field, ref shortened);
            RuleUid = Sanitizer.SanitizeOpt(RuleUid, ref shortened);
            GroupName = Sanitizer.SanitizeOpt(GroupName, ref shortened);
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            return shortened;
        }
    }
}
