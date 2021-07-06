using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class Group<T>
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("byId")]
        public T Object { get; set; }
    }
}
