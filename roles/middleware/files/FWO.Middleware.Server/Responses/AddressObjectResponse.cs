using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class AddressObjectResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
