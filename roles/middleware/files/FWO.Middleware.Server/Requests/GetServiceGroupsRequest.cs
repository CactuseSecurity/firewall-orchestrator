using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetServiceGroupsRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();
}
