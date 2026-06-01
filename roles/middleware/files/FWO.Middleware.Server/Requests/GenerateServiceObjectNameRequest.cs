using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GenerateServiceObjectNameRequest
{
    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("typ")]
    public string Typ { get; set; } = string.Empty;
}
