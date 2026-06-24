using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the NetObjectValidityResponse type.
/// </summary>
public sealed class NetObjectValidityResponse
{
    /// <summary>
    /// Gets the IsValid value.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
}
