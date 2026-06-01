using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class GenerateAddressObjectNameResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
