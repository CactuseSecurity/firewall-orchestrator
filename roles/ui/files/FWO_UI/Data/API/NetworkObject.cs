using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class NetworkObject
    {
        [JsonPropertyName("obj_id")]
        public int Id { get; set; }

        [JsonPropertyName("obj_name")]
        public string Name { get; set; }

        [JsonPropertyName("obj_ip")]
        public string IP { get; set; }

        [JsonPropertyName("obj_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("zone")]
        public NetworkZone Zone { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("obj_create")]
        public int Create { get; set; }

        [JsonPropertyName("obj_last_seen")]
        public int LastSeen { get; set; }

        [JsonPropertyName("type")]
        public NetworkObjectType Type { get; set; }

        [JsonPropertyName("obj_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("obj_member_names")]
        public string MemberNames { get; set; }

        [JsonPropertyName("obj_member_refs")]
        public string MemberRefs { get; set; }

        [JsonPropertyName("objgrps")]
        public Group<NetworkObject>[] ObjectGroups { get; set; }

        [JsonPropertyName("objgrp_flats")]
        public GroupFlat<NetworkObject>[] ObjectGroupFlats { get; set; }

        //    obj_id
        //    obj_name
        //    obj_ip
        //    obj_ip_end
        //    obj_uid
        //    zone_id <---
        //    active
        //    obj_create
        //    obj_last_seen
        //    type: stm_obj_typ {
        //      name: obj_typ_name
        //    }
        //    obj_comment
        //    obj_member_names
        //    obj_member_refs
        //    objgrps
        //    {
        //        objgrp_member_id
        //      objectByObjgrpMemberId
        //        {
        //            obj_id
        //            obj_name
        //      }
        //    }
        //    objgrp_flats {
        //      objgrp_flat_id
        //      objectByObjgrpFlatMemberId
        //      {
        //          obj_id
        //          obj_name
        //      }
        //    }
    }
}
