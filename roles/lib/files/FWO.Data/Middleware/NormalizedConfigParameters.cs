
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Middleware
{
    public class NormalizedConfigGetParameters
    {
        [JsonProperty("mgm-ids")]
        public int[] ManagementIds { get; set; } = [];

        [JsonProperty("config=time")]
        public string? ConfigTime { get; set; }
    }
}