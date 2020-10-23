using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Zone
    {
        [JsonPropertyName("zone_name")]
        public string Name { get; set; }

        [JsonPropertyName("zone_id")]
        public int Id { get; set; }
    }
}
