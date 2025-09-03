using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedNetworkObject
    {
        [JsonProperty("obj_uid"), JsonPropertyName("obj_uid")]
        public string ObjUid { get; set; } = "";

        [JsonProperty("obj_name"), JsonPropertyName("obj_name")]
        public string ObjName { get; set; } = "";

        [JsonProperty("obj_ip"), JsonPropertyName("obj_ip")]
        public string ObjIp { get; set; } = "";

        [JsonProperty("obj_ip_end"), JsonPropertyName("obj_ip_end")]
        public string ObjIpEnd { get; set; } = "";

        [JsonProperty("obj_color"), JsonPropertyName("obj_color")]
        public string ObjColor { get; set; } = "";

        [JsonProperty("obj_typ"), JsonPropertyName("obj_typ")]
        public string ObjType { get; set; } = "";

        [JsonProperty("obj_member_refs"), JsonPropertyName("obj_member_refs")]
        public string? ObjMemberRefs { get; set; }

        [JsonProperty("obj_member_names"), JsonPropertyName("obj_member_names")]
        public string? ObjMemberNames { get; set; }

        [JsonProperty("obj_comment"), JsonPropertyName("obj_comment")]
        public string? ObjComment { get; set; }

        /// <summary>
        /// Creates a NormalizedNetworkObject from a NetworkObject.
        /// </summary>
        /// <param name="networkObject">The NetworkObject to normalize.</param>
        /// <returns>A normalized NetworkObject.</returns>
        public static NormalizedNetworkObject FromNetworkObject(NetworkObject networkObject)
        {
            return new NormalizedNetworkObject
            {
                ObjUid = networkObject.Uid,
                ObjName = networkObject.Name,
                ObjIp = networkObject.IP,
                ObjIpEnd = networkObject.IpEnd,
                ObjColor = networkObject.Color.Name,
                ObjType = networkObject.Type.Name,
                ObjMemberRefs = networkObject.MemberRefs,
                ObjMemberNames = networkObject.MemberNames,
                ObjComment = networkObject.Comment
            };
        }
    }
}
