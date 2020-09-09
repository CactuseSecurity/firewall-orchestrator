using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Management
    {
        [JsonPropertyName("mgm_id")]
        public int Id { get; set; }

        [JsonPropertyName("mgm_name")]
        public string Name { get; set; }

        [JsonPropertyName("devices")]
        public Device[] Devices { get; set; }
    }
}
