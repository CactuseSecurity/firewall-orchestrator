using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class DeviceWrapper
    {
        [JsonProperty("device"), JsonPropertyName("device")]
        public Device Content { get; set; } = new Device();
    }
}
