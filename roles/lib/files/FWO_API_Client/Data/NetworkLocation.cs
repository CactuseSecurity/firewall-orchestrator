using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkLocation
    {
        [JsonPropertyName("object")]
        public NetworkObject Object { get; set; }

        [JsonPropertyName("usr")]
        public NetworkUser User { get; set; }
    }
}
