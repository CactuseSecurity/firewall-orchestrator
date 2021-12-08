using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkLocation
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; }

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser User { get; set; }
    }
}
