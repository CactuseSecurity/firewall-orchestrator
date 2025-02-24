using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class UserWrapper
    {
        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser Content { get; set; } = new NetworkUser();
    }
}
