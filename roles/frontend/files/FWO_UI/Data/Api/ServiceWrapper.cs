using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.Api
{
    public class ServiceWrapper
    {
        [JsonPropertyName("service")]
        public Service Content { get; set; }
    }
}
