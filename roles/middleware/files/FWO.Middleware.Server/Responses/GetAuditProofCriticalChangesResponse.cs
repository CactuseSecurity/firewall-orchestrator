using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetAuditProofCriticalChangesResponse type.
/// </summary>
public sealed class GetAuditProofCriticalChangesResponse
{
    /// <summary>
    /// Gets or sets the audit proof critical changes of the ticket, newest first. Empty when the
    /// ticket carries none or the supplied filter excludes all of them; a ticket that does not
    /// exist is reported as 404 instead of an empty list.
    /// </summary>
    [JsonPropertyName("changes")]
    public List<AuditProofCriticalChangeResponse> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets the task-state evidence associated with the returned audit-proof changes.
    /// </summary>
    /// <remarks>
    /// Null when no audit-proof changes match the request. Request-task diffs compare the first
    /// recorded insert snapshot with the current request-task state; they describe the ticket as a
    /// whole and are therefore not restricted by the filter. Implementation-task changes contain the
    /// audit-proof-critical manual history entries that match the filter.
    /// </remarks>
    [JsonPropertyName("taskDiff")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuditProofTaskDiffResponse? TaskDiff { get; set; }
}

/// <summary>
/// Represents the task-state evidence returned with a non-empty audit-proof trail.
/// </summary>
public sealed class AuditProofTaskDiffResponse
{
    /// <summary>
    /// Gets or sets the request-task snapshots whose current content differs from the original request.
    /// </summary>
    /// <remarks>
    /// Start, stop and additional info are written by workflow actions and do not count as a
    /// difference; the order of owners and elements does not matter either. The snapshots themselves
    /// are returned complete.
    /// </remarks>
    [JsonPropertyName("requestTaskDiffs")]
    public List<RequestTaskDiffResponse> RequestTaskDiffs { get; set; } = [];

    /// <summary>
    /// Gets or sets the manual implementation-task changes that are audit-proof critical.
    /// </summary>
    [JsonPropertyName("manualImplementationTaskChanges")]
    public List<ManualImplementationTaskChangeResponse> ManualImplementationTaskChanges { get; set; } = [];
}

/// <summary>
/// Represents the original and current snapshots of one request task.
/// </summary>
public sealed class RequestTaskDiffResponse
{
    /// <summary>Gets or sets the database id of the request task.</summary>
    [JsonPropertyName("requestTaskId")]
    public long RequestTaskId { get; set; }

    /// <summary>Gets or sets the snapshot captured when the request task was created.</summary>
    [JsonPropertyName("original")]
    public JsonElement Original { get; set; }

    /// <summary>Gets or sets the current snapshot, or null when the task was deleted.</summary>
    [JsonPropertyName("current")]
    public JsonElement? Current { get; set; }
}

/// <summary>
/// Represents one manual, audit-proof-critical implementation-task change.
/// </summary>
public sealed class ManualImplementationTaskChangeResponse
{
    /// <summary>Gets or sets the database id of the implementation task.</summary>
    [JsonPropertyName("implementationTaskId")]
    public long ImplementationTaskId { get; set; }

    /// <summary>Gets or sets the recorded time of the change.</summary>
    [JsonPropertyName("changeTime")]
    public DateTime? ChangeTime { get; set; }

    /// <summary>Gets or sets the authenticated user's database id, when available.</summary>
    [JsonPropertyName("changeUserId")]
    public int? ChangeUserId { get; set; }

    /// <summary>Gets or sets the free-text name recorded for the user.</summary>
    [JsonPropertyName("changeUserName")]
    public string ChangeUserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the recorded description of the change.</summary>
    [JsonPropertyName("changeContent")]
    public string ChangeContent { get; set; } = string.Empty;

    /// <summary>Gets or sets the snapshot before the manual change.</summary>
    [JsonPropertyName("original")]
    public JsonElement? Original { get; set; }

    /// <summary>Gets or sets the snapshot after the manual change.</summary>
    [JsonPropertyName("current")]
    public JsonElement? Current { get; set; }
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
