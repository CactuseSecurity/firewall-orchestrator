
using System.Text.Json.Serialization; 

namespace FWO.Data.Middleware
{
    public class NormalizedConfigGetParameters
    {
        [JsonPropertyName("mgm-ids")]
        public int[] ManagementIds { get; set; } = [];

        [JsonPropertyName("config-time")]
        public string? ConfigTime { get; set; }
    }
}