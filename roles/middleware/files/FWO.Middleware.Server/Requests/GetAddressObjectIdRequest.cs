using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetAddressObjectIdRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();

    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    public sealed class VisibleInRequestFilter
    {
        [JsonPropertyName("visibleInRequest")]
        public bool VisibleInRequest { get; set; } = true;
    }
}
