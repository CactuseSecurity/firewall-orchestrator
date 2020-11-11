using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Auth.Server.Data
{
    public class UserOrig
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public string Dn { get; set; }
        public Tenant Tenant { get; set; }
        public string DefaultRole { get; set; }
        public string[] Roles { get; set; }
    }
}
