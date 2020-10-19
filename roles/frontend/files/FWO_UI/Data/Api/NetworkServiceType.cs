using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.Api
{
    public class NetworkServiceType
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
