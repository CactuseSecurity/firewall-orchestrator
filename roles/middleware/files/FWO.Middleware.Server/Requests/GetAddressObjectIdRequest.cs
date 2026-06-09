using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetAddressObjectIdRequest : IVisibleInRequestFilterRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
