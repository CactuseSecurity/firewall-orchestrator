using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkZone
    {
        [JsonPropertyName("zone_id")]
        public int Id { get; set; }

        [JsonPropertyName("zone_name")]
        public string Name { get; set; }
    }
}
