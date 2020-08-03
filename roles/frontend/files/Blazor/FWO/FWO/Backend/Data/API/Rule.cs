using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Rule
    {
        [JsonPropertyName("rule_num_numeric")]
        public decimal Number { get; set; }  

        [JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonPropertyName("rule_services")]
        public Service[] Services { get; set; }

        [JsonPropertyName("rule_src")]
        public string Source { get; set; }
        
        [JsonPropertyName("rule_dst")]
        public string Destination { get; set; }
    }
}
