using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetServiceObjectsRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();
}
