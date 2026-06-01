using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class NetGroupValidityResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
}
