using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents every validation error detected in one request. Validation does not stop at the
/// first error, so a caller can correct all of them in a single round trip.
/// </summary>
public sealed class RequestValidationErrorResponse
{
    /// <summary>
    /// Gets or sets the detected errors, each attributed to the request path that caused it.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<RequestValidationError> Errors { get; set; } = [];
}

/// <summary>
/// Represents one validation error and the request path it belongs to.
/// </summary>
public sealed class RequestValidationError
{
    /// <summary>
    /// Gets or sets the request path of the offending key, for example <c>ticketId</c> or
    /// <c>options.filter.changeTime</c>. Empty for an error on the request root.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
