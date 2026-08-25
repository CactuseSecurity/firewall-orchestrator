using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the CreateRequestResponse type.
/// </summary>
public sealed class CreateRequestResponse
{
    /// <summary>
    /// Gets the Status value.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets the RequestId value.
    /// </summary>
    [JsonPropertyName("requestId")]
    public long RequestId { get; set; }
}
