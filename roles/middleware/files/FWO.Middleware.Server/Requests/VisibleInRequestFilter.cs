using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class VisibleInRequestFilter
{
    [JsonPropertyName("visibleInRequest")]
    public bool? VisibleInRequest { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
