using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkServiceType
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
