using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetNetObjectValidityRequest
{
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("netMask")]
    public int NetMask { get; set; }

    [JsonPropertyName("minPrefixLength")]
    public int MinPrefixLength { get; set; }
}
