using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class TimeWrapper
    {
        [JsonProperty("time"), JsonPropertyName("time")]
        public DateTime Time { get; set; }
    }
}
