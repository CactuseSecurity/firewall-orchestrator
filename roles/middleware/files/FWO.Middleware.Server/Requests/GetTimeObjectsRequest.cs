using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetTimeObjectsRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();
}
