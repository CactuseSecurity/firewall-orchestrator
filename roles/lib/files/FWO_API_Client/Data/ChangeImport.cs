using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class ChangeImport
    {
        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        public ChangeImport() {}
        
        public ChangeImport(ChangeImport changeImport)
        {
            Time = changeImport.Time;
        }
    }
}
