using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkObjectType
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
