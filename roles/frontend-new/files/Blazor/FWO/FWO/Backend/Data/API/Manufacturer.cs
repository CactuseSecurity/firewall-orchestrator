using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Manufacturer
    {
        public readonly string Name;

        public readonly Management[] Managements;

        public Manufacturer(string Name, Management[] Managements)
        {
            this.Name = Name;
            this.Managements = Managements;
        }
    }
}
