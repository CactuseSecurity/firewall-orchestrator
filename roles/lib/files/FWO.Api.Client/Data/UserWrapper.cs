using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class UserWrapper
    {
        [JsonPropertyName("usr")]
        public NetworkUser Content { get; set; } = new NetworkUser();
    }
}
