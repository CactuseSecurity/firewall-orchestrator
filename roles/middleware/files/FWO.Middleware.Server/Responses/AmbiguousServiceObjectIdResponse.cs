using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Returned with 409 when a service object id lookup matches more than one service object.
/// </summary>
public sealed class AmbiguousServiceObjectIdResponse
{
    /// <summary>
    /// Gets the explanation of the conflict.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the matching service objects ordered by id, at most 20.
    /// </summary>
    [JsonPropertyName("candidates")]
    public List<ServiceObjectIdResponse> Candidates { get; set; } = [];
}
