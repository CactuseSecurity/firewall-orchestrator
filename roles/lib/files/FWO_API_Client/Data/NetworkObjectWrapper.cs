using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class NetworkObjectWrapper
    {
        [JsonPropertyName("object")]
        public NetworkObject Content { get; set; }
    }
}
