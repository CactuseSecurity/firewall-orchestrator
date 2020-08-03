using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Service
    {
        [JsonPropertyName("svc_name")]
        public string Name { get; set; }

        [JsonPropertyName("svc_port")]
        public int Port { get; set; }
    }
}
