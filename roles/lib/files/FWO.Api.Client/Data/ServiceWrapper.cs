using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class ServiceWrapper
    {
        [JsonPropertyName("service")]
        public NetworkService Content { get; set; }
    }
    // public class ServiceObjectRecursiveWrapper
    // {
    //     [JsonPropertyName("service")]
    //     public ServiceObjectRecursiveFlatsWrapper Content { get; set; }
    // }
}
