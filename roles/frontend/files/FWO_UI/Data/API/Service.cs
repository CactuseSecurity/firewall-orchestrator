using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.Api
{
    public class Service
    {
        [JsonPropertyName("svc_name")]
        public string Name { get; set; }

        [JsonPropertyName("svc_port")]
        public int? Port { get; set; }

        [JsonPropertyName("stm_svc_typ")]
        public ServiceType Type { get; set; }
    }
}
