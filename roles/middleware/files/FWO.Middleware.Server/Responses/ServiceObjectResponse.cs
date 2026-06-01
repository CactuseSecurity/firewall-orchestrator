using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class ServiceObjectResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
