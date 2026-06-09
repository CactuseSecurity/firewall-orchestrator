using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetServiceGroupsRequest : IVisibleInRequestFilterRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
