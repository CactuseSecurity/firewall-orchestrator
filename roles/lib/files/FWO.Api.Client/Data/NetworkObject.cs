using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
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
        public NetworkZone Zone { get; set; } = new NetworkZone(){};

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonProperty("obj_create"), JsonPropertyName("obj_create")]
        public int Create { get; set; }

        [JsonProperty("obj_create_time"), JsonPropertyName("obj_create_time")]
        public TimeWrapper CreateTime { get; set; } = new TimeWrapper(){};

        [JsonProperty("obj_last_seen"), JsonPropertyName("obj_last_seen")]
        public int LastSeen { get; set; }

        [JsonProperty("type"), JsonPropertyName("type")]
        public NetworkObjectType Type { get; set; } = new NetworkObjectType(){};

        [JsonProperty("obj_comment"), JsonPropertyName("obj_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("obj_member_names"), JsonPropertyName("obj_member_names")]
        public string MemberNames { get; set; } = "";

        [JsonProperty("obj_member_refs"), JsonPropertyName("obj_member_refs")]
        public string MemberRefs { get; set; } = "";

        [JsonProperty("objgrps"), JsonPropertyName("objgrps")]
        public Group<NetworkObject>[] ObjectGroups { get; set; } = new Group<NetworkObject>[]{};

        [JsonProperty("objgrp_flats"), JsonPropertyName("objgrp_flats")]
        public GroupFlat<NetworkObject>[] ObjectGroupFlats { get; set; } = new GroupFlat<NetworkObject>[]{};

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
    }
}
