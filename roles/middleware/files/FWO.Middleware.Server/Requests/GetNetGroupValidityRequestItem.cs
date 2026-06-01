using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetNetGroupValidityRequestItem
{
    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;
}
