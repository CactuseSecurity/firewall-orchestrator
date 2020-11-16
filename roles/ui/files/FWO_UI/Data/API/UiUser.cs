using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Ui.Data.API
{
    public class UiUser
    {
        [JsonPropertyName("uiuser_username")]
        public string Name { get; set; }

        [JsonPropertyName("uiuser_id")]
        public int DbId { get; set; }

        [JsonPropertyName("uuid")]
        public string Dn { get; set; }

            
        public UiUser()
        {}
        
        public UiUser(UiUser user)
        {
            Name = user.Name;
            DbId = user.DbId;
            Dn = user.Dn;
        }
    }
}
