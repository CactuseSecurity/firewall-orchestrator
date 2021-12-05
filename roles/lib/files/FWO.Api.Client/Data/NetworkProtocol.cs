using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkProtocol
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
