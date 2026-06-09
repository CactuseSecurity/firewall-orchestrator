using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetAddressGroupsRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();
}
