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

        [JsonPropertyName("obj_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("obj_ip")]
        public string IP { get; set; }

        [JsonPropertyName("obj_name")]
        public string Name { get; set; }

        //    obj_id
        //    obj_name
        //    obj_ip
        //    obj_ip_end
        //    obj_uid
        //    zone_id
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
        //    {
        //        obj_id
        //        obj_name
        //    }
        //}
    }
}
