using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class NetworkObjectWrapper
    {
        [JsonPropertyName("object")]
        public NetworkObject Content { get; set; }
    }
}
