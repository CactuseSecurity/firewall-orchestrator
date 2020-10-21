using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class NetworkUser
    {
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

        [JsonPropertyName("user_typ_id")]
        public string Id { get; set; }

        [JsonPropertyName("user_member_names")]
        public string MemberNames { get; set; }

        [JsonPropertyName("user_member_refs")]
        public string MemberRefs { get; set; }

        [JsonPropertyName("stm_usr_typ")]
        public NetworkUserType Type { get; set; }
    }
}
