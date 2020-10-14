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

  //      [JsonPropertyName("user_comment")]
  //user_comment
  //user_lastname
  //user_firstname
  //usr_typ_id
  //stm_usr_typ
  //      {
  //          usr_typ_name
  //      }
  //      user_member_names
  //      user_member_refs
    }
}
