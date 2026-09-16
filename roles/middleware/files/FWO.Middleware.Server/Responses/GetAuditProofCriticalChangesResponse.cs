using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetAuditProofCriticalChangesResponse type.
/// </summary>
public sealed class GetAuditProofCriticalChangesResponse
{
    /// <summary>
    /// Gets or sets the audit proof critical changes of the ticket, newest first. Empty when the
    /// ticket has none, or does not exist.
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
    /// Gets or sets the time the change was recorded, without a UTC offset.
    /// </summary>
    /// <remarks>
    /// The underlying column is a timezone-naive timestamp, so the value carries the wall clock of
    /// the database server and no offset can be stated for it. A reader that needs an absolute
    /// instant has to apply the timezone of the installation. Null when the stored row carries no
    /// timestamp; such rows sort last.
    /// </remarks>
    [JsonPropertyName("changeTime")]
    public DateTime? ChangeTime { get; set; }

    /// <summary>
    /// Gets or sets the name recorded for the user who performed the change.
    /// </summary>
    /// <remarks>
    /// Free text supplied by the writer of the change and therefore not trustworthy on its own. Use
    /// <see cref="ChangeUserId"/> to attribute a change; this field is the only attribution
    /// available where that id is null. Empty when the stored row carries no name.
    /// </remarks>
    [JsonPropertyName("changeUserName")]
    public string ChangeUserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database id of the user who performed the change.
    /// </summary>
    /// <remarks>
    /// Preset by the API from the authenticated session, so unlike <see cref="ChangeUserName"/> it
    /// cannot be chosen by the writer. Null marks a change made outside a user session, that is by
    /// automation such as a background job.
    /// </remarks>
    [JsonPropertyName("changeUserId")]
    public int? ChangeUserId { get; set; }

    /// <summary>
    /// Gets or sets the recorded description of the change. Empty when the stored row carries none.
    /// </summary>
    [JsonPropertyName("changeContent")]
    public string ChangeContent { get; set; } = string.Empty;
}
