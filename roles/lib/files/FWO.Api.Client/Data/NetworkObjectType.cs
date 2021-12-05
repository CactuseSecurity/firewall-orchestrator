using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkObjectType
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
