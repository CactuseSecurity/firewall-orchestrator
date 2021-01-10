using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.ApiClient
{
    public class ReturnId
    {
        [JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonPropertyName("UpdatedId")]
        public int UpdatedId { get; set; }

        [JsonPropertyName("DeletedId")]
        public int DeletedId { get; set; }

        [JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }
    }
    
    public class NewReturning
    {
        [JsonPropertyName("returning")]
        public ReturnId[] ReturnIds { get; set; }
    }
}
