using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server
{
    public class User
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public string Dn { get; set; }
        public Tenant Tenant { get; set; }
        public string DefaultRole { get; set; }
        public string[] Roles { get; set; }
    }
}
