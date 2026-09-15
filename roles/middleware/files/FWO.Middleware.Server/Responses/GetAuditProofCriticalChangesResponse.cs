using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetAuditProofCriticalChangesResponse type.
/// </summary>
public sealed class GetAuditProofCriticalChangesResponse
{
    /// <summary>
    /// Gets or sets the audit proof critical changes of the ticket, newest first. Empty when the ticket has
    /// none, or does not exist.
    /// </summary>
    [JsonPropertyName("changes")]
    public List<AuditProofCriticalChangeResponse> Changes { get; set; } = [];
}

/// <summary>
/// Represents one audit proof critical change of a workflow ticket.
/// </summary>
public sealed class AuditProofCriticalChangeResponse
{
    /// <summary>
    /// Gets or sets the time the change was recorded. Null when the stored row carries no
    /// timestamp.
    /// </summary>
    [JsonPropertyName("changeTime")]
    public DateTime? ChangeTime { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who performed the change.
    /// </summary>
    [JsonPropertyName("changeUserName")]
    public string ChangeUserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recorded description of the change.
    /// </summary>
    [JsonPropertyName("changeContent")]
    public string ChangeContent { get; set; } = string.Empty;
}
