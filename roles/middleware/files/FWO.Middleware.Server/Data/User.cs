using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Data
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

        
        public User()
        {}
        
        public User(User user)
        {
            Name = user.Name;
            DbId = user.DbId;
            Password = user.Password;
            Dn = user.Dn;
            Tenant = user.Tenant;
            DefaultRole = user.DefaultRole;
            Roles = user.Roles;
        }
    }
}
