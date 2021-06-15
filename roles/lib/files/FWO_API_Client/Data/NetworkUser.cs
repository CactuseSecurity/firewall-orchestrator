using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkUser
    {
        [JsonPropertyName("user_id")]
        public int Id { get; set; }

        [JsonPropertyName("user_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("user_name")]
        public string Name { get; set; }

        [JsonPropertyName("user_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("user_lastname")]
        public string LastName { get; set; }

        [JsonPropertyName("user_firstname")]
        public string FirstName { get; set; }

        [JsonPropertyName("usr_typ_id")]
        public int TypeId { get; set; }

        [JsonPropertyName("type")]
        public NetworkUserType Type { get; set; }

        [JsonPropertyName("user_create")]
        public int Create { get; set; }

        [JsonPropertyName("user_last_seen")]
        public int LastSeen { get; set; }

        [JsonPropertyName("user_member_names")]
        public string MemberNames { get; set; }

        [JsonPropertyName("user_member_refs")]
        public string MemberRefs { get; set; }

        [JsonPropertyName("usergrps")]
        public Group<NetworkUser>[] UserGroups { get; set; }

        [JsonPropertyName("usergrp_flats")]
        public GroupFlat<NetworkUser>[] UserGroupFlats { get; set; }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case NetworkUser user:
                    return Id == user.Id;
                default:
                    return base.Equals(obj);

            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        //  user_id
        //  user_uid
        //  user_name
        //  user_comment
        //  user_lastname
        //  user_firstname
        //  usr_typ_id
        //  stm_usr_typ {
        //    usr_typ_name
        //  }
        //  user_member_names
        //  user_member_refs
        //  usergrps {
        //    id: usergrp_id
        //    byId: usrByUsergrpMemberId {
        //      user_id
        //      user_name
        //    }
        //  }
        //  usergrp_flats {
        //    flat_id: usergrp_flat_id
        //    byFlatId: usrByUsergrpFlatMemberId {
        //      user_id
        //      user_name
        //    }
        //  }

    }
}
