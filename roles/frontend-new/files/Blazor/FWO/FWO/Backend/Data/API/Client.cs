using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Client
    {
        public readonly string Name;

        public readonly Manufacturer[] Manufacturers;

        public Client(string Name, Manufacturer[] Manufacturers)
        {
            this.Name = Name;
            this.Manufacturers = Manufacturers;
        }
    }
}
