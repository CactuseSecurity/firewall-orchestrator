using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class FilterData
    {
        public string Name { get; set; }

        public AttributeDefinition[] AttributeDefinitions { get; set; }

        public FilterDataObject[] FilterObjects { get; set; }
    }
}
