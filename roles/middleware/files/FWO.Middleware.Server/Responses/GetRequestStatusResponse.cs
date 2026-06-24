using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetRequestStatusResponse type.
/// </summary>
public sealed class GetRequestStatusResponse
{
    /// <summary>
    /// Gets the Status value.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
