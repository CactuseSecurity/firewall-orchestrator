using NetTools;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.Data.Flow;

namespace FWO.Data
{
    public class NetworkObject
    {
        [JsonProperty("obj_id"), JsonPropertyName("obj_id")]
        public long Id { get; set; }

        [JsonProperty("obj_name"), JsonPropertyName("obj_name")]
        public string Name { get; set; } = "";

        [JsonProperty("obj_ip"), JsonPropertyName("obj_ip")]
        public string IP { get; set; } = "";

        [JsonProperty("obj_ip_end"), JsonPropertyName("obj_ip_end")]
        public string IpEnd { get; set; } = "";

        [JsonProperty("obj_uid"), JsonPropertyName("obj_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("zone"), JsonPropertyName("zone")]
        public NetworkZone? Zone { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonProperty("obj_create"), JsonPropertyName("obj_create")]
        public int Create { get; set; }

        [JsonProperty("obj_create_time"), JsonPropertyName("obj_create_time")]
        public TimeWrapper CreateTime { get; set; } = new();

        [JsonProperty("type"), JsonPropertyName("type")]
        public NetworkObjectType Type { get; set; } = new();

        [JsonProperty("obj_color"), JsonPropertyName("obj_color")]
        public Color? Color { get; set; }

        [JsonProperty("obj_comment"), JsonPropertyName("obj_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("obj_member_names"), JsonPropertyName("obj_member_names")]
        public string MemberNames { get; set; } = "";

        [JsonProperty("obj_member_refs"), JsonPropertyName("obj_member_refs")]
        public string MemberRefs { get; set; } = "";

        [JsonProperty("objgrps"), JsonPropertyName("objgrps")]
        public Group<NetworkObject>[] ObjectGroups { get; set; } = [];

        [JsonProperty("objgrp_flats"), JsonPropertyName("objgrp_flats")]
        public GroupFlat<NetworkObject>[] ObjectGroupFlats { get; set; } = [];

        [JsonProperty("flow_nwobj_id"), JsonPropertyName("flow_nwobj_id")]
        public long? FlowNetworkObjectId { get; set; }

        [JsonProperty("flow_nwobject"), JsonPropertyName("flow_nwobject")]
        public FlowNwObject? FlowNwObject { get; set; }

        [JsonProperty("flow_nwgrp_id"), JsonPropertyName("flow_nwgrp_id")]
        public long? FlowNetworkGroupId { get; set; }

        [JsonProperty("flow_nwgroup"), JsonPropertyName("flow_nwgroup")]
        public FlowNwGroup? FlowNwGroup { get; set; }

        [JsonProperty("flow_active"), JsonPropertyName("flow_active")]
        public bool FlowActive { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long? Removed { get; set; }

        public long Number;
        public bool Highlighted = false;
        public bool IsSurplus = false;

        /// <summary>
        /// List of IP ranges that overlap with matched owner ranges.
        /// Used for IP-based owner mapping.
        /// </summary>
        [JsonProperty("overlapping_ranges", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("overlapping_ranges")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<IPAddressRange>? OverlappingRanges { get; set; }


        public override bool Equals(object? obj)
        {
            return obj switch
            {
                NetworkObject nobj => Id == nobj.Id,
                _ => base.Equals(obj),
            };
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool IsAnyObject()
        {
            return IP == "0.0.0.0/32" && IpEnd == "255.255.255.255/32" ||
                IP == "::/128" && IpEnd == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128";
        }

        public static List<NetworkObject> FlattenRuleNetworkObjects(IEnumerable<NetworkObject> objects)
        {
            return objects
                .SelectMany(obj =>
                    new[] { obj }
                        .Concat(obj.ObjectGroupFlats.Select(groupFlat => groupFlat.Object)))
                .OfType<NetworkObject>()
                .ToList();
        }
    }
}
