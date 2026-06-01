using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GenerateAddressObjectNameRequest
{
    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    [JsonPropertyName("netMask")]
    public int NetMask { get; set; }
}
