using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class GroupFlat<T>
    {
        [JsonPropertyName("flat_id")]
        public int Id { get; set; }

        [JsonPropertyName("byFlatId")]
        public T Object { get; set; }
    }
}
