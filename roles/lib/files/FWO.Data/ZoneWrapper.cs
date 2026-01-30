using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Data
{
    public class ZoneWrapper
    {
        [JsonProperty("zone"), JsonPropertyName("zone")]
        public NetworkZone Content { get; set; } = new NetworkZone();
    }
}
