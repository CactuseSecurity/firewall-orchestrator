using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetServiceObjectIdRequest : IVisibleInRequestFilterRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
