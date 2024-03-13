using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkUser
    {
        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public long Id { get; set; }

        [JsonProperty("user_uid"), JsonPropertyName("user_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("user_name"), JsonPropertyName("user_name")]
        public string Name { get; set; } = "";

        [JsonProperty("user_comment"), JsonPropertyName("user_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("user_lastname"), JsonPropertyName("user_lastname")]
        public string LastName { get; set; } = "";

        [JsonProperty("user_firstname"), JsonPropertyName("user_firstname")]
        public string FirstName { get; set; } = "";

        [JsonProperty("usr_typ_id"), JsonPropertyName("usr_typ_id")]
        public int TypeId { get; set; }

        [JsonProperty("type"), JsonPropertyName("type")]
        public NetworkUserType Type { get; set; } = new(){};

        [JsonProperty("user_create"), JsonPropertyName("user_create")]
        public int Create { get; set; }

        [JsonProperty("user_create_time"), JsonPropertyName("user_create_time")]
        public TimeWrapper CreateTime { get; set; } = new(){};

        [JsonProperty("user_last_seen"), JsonPropertyName("user_last_seen")]
        public int LastSeen { get; set; }

        [JsonProperty("user_member_names"), JsonPropertyName("user_member_names")]
        public string MemberNames { get; set; } = "";

        [JsonProperty("user_member_refs"), JsonPropertyName("user_member_refs")]
        public string MemberRefs { get; set; } = "";

        [JsonProperty("usergrps"), JsonPropertyName("usergrps")]
        public Group<NetworkUser>[] UserGroups { get; set; } = new Group<NetworkUser>[]{};

        [JsonProperty("usergrp_flats"), JsonPropertyName("usergrp_flats")]
        public GroupFlat<NetworkUser>[] UserGroupFlats { get; set; } = new GroupFlat<NetworkUser>[]{};

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                NetworkUser user => Id == user.Id,
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
