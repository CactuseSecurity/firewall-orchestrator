using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Management
    {
        public readonly string Name;

        public readonly Firewall[] Firewalls;

        public Management(string Name, Firewall[] Firewalls)
        {
            this.Name = Name;
            this.Firewalls = Firewalls;
        }
    }
}
