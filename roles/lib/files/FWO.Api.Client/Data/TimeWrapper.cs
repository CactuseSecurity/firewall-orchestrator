using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class TimeWrapper
    {
        [JsonProperty("time"), JsonPropertyName("time")]
        public DateTime Time { get; set; }
    }
}
