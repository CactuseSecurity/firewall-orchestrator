using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class ReturnId
    {
        [JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonPropertyName("UpdatedId")]
        public int UpdatedId { get; set; }

        [JsonPropertyName("DeletedId")]
        public int DeletedId { get; set; }
    }
    
    public class NewReturning
    {
        [JsonPropertyName("returning")]
        public ReturnId[] ReturnIds { get; set; }
    }
}
