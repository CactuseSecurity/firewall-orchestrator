using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Backend.Data.API
{
    public class Device
    {
        [JsonPropertyName("dev_id")]
        public int Id { get; set; }

        [JsonPropertyName("dev_name")]
        public string Name { get; set; }

        [JsonPropertyName("rules")]
        public Rule[] Rules { get; set; }
    }
}
