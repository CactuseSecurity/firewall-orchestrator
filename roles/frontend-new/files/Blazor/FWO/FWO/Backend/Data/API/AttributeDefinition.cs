using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class AttributeDefinition
    {
        public string Name { get; set; }

        public JsonValueKind Type { get; set; }
    }
}
