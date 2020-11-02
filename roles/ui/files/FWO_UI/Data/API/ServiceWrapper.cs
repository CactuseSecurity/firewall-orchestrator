using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class ServiceWrapper
    {
        [JsonPropertyName("service")]
        public NetworkService Content { get; set; }
    }
}
