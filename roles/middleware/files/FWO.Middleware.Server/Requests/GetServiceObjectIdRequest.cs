using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetServiceObjectIdRequest
{
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter Filter { get; set; } = new();

    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    public sealed class VisibleInRequestFilter
    {
        [JsonPropertyName("visibleInRequest")]
        public bool VisibleInRequest { get; set; } = true;
    }
}
