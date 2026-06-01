using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class NetObjectValidityResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
}
