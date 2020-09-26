using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server
{
    public class Role
    {
        public string Name { get; set; }

        public Role()
        {

        }

        public Role(string Name)
        {
            this.Name = Name;
        }
    }
}
