using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Firewall
    {
        public readonly string Name;

        public readonly Rule[] Rules;

        public Firewall(string Name, Rule[] Rules)
        {
            this.Name = Name;
            this.Rules = Rules;
        }
    }
}
