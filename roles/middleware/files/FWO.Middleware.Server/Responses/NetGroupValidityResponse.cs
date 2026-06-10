using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the NetGroupValidityResponse type.
/// </summary>
public sealed class NetGroupValidityResponse
{
    /// <summary>
    /// Gets the IsValid value.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
}
