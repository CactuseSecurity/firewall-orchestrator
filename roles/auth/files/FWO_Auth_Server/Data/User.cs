using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Auth.Server.Data
{
    public class User
    {
        [JsonPropertyName("uiuser_username")]
        public string Name { get; set; }

        [JsonPropertyName("uiuser_id")]
        public int DbId { get; set; }

        public string Password { get; set; }

        [JsonPropertyName("uuid")]
        public string Dn { get; set; }

        public Tenant Tenant { get; set; }
        
        public string DefaultRole { get; set; }
        
        public string[] Roles { get; set; }
    }
}
