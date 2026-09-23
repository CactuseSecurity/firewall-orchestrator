using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides workflow change history data for the workflow REST endpoints.
/// </summary>
public sealed class WorkflowChangeHistoryService
{
    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    /// <param name="apiConnection">Shared internal API connection, which runs as middleware-server.</param>
    public WorkflowChangeHistoryService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Returns the audit proof critical changes of one workflow ticket, newest first.
    /// </summary>
    /// <param name="ticketId">Database id of the workflow ticket.</param>
    /// <param name="filter">Optional response filter; null applies no restriction.</param>
    /// <returns>
    /// The matching changes, empty when the ticket carries none or the filter excludes all of them;
    /// null when no workflow ticket with that id exists, which the caller reports as not found.
    /// </returns>
    /// <remarks>
    /// The ticket restriction is applied by the GraphQL query, the response filter in memory: the
    /// result set of a single ticket is small, and matching here keeps the filter semantics of the
    /// contract in one place instead of splitting them across query variables.
    /// <para>
    /// The existence of the ticket is probed only when the change query came back empty: any
    /// returned change already proves the ticket exists, so the common case still costs one round
    /// trip. The filter is deliberately not part of that decision - a filter that excludes every
    /// change of an existing ticket is an empty result, not a missing ticket.
    /// </para>
    /// </remarks>
    public async Task<GetAuditProofCriticalChangesResponse?> GetAuditProofCriticalChangesAsync(long ticketId, AuditProofCriticalChangeFilter? filter)
    {
        List<ModellingHistoryEntry> entries = await apiConnection.SendQueryAsync<List<ModellingHistoryEntry>>(
            RequestQueries.getAuditProofCriticalChangesForTicket, new { ticketId });

        if (entries.Count == 0 && !await TicketExistsAsync(ticketId))
        {
            return null;
        }

        List<AuditProofCriticalChangeResponse> changes = entries.Select(Map).Where(change => Matches(change, filter)).ToList();
        AuditProofTaskDiffResponse? taskDiff = changes.Count == 0
            ? null
            : await GetTaskDiffAsync(ticketId);

        return new GetAuditProofCriticalChangesResponse
        {
            Changes = changes,
            TaskDiff = taskDiff
        };
    }

    /// <summary>
    /// Builds the task-state evidence attached to a non-empty audit-proof trail.
    /// </summary>
    /// <param name="ticketId">Database id of the workflow ticket.</param>
    /// <returns>The request-task diff and the manual implementation-task history entries.</returns>
    private async Task<AuditProofTaskDiffResponse> GetTaskDiffAsync(long ticketId)
    {
        List<ModellingHistoryEntry> history = await apiConnection.SendQueryAsync<List<ModellingHistoryEntry>>(
            RequestQueries.getWorkflowTaskHistoryForTicket, new { ticketId });
        WfTicket currentTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });

        return new AuditProofTaskDiffResponse
        {
            RequestTaskDiffs = BuildRequestTaskDiffs(history, currentTicket.Tasks),
            ManualImplementationTaskChanges = BuildManualImplementationTaskChanges(history)
        };
    }

    /// <summary>
    /// Compares each request task's creation snapshot with its current database state.
    /// </summary>
    /// <param name="history">Chronologically ordered workflow task history for one ticket.</param>
    /// <param name="currentTasks">Current request tasks loaded from the ticket.</param>
    /// <returns>Only request tasks whose current state has changed since creation.</returns>
    private static List<RequestTaskDiffResponse> BuildRequestTaskDiffs(List<ModellingHistoryEntry> history, List<WfReqTask> currentTasks)
    {
        return history.Where(entry => entry.ObjectType == (int)ChangeHistoryObjectType.RequestTask)
            .GroupBy(entry => entry.ObjectId)
            .Select(entries => BuildRequestTaskDiff(entries, currentTasks.FirstOrDefault(task => task.Id == entries.Key)))
            .Where(diff => diff != null)
            .Select(diff => diff!)
            .ToList();
    }

    /// <summary>
    /// Builds one request-task diff when its history includes a creation snapshot and a change.
    /// </summary>
    /// <param name="entries">Chronologically ordered history entries for one request task.</param>
    /// <param name="currentTask">Current request-task state, or null when the task was deleted.</param>
    /// <returns>The diff, or null when the original state is unavailable or unchanged.</returns>
    private static RequestTaskDiffResponse? BuildRequestTaskDiff(IGrouping<long, ModellingHistoryEntry> entries, WfReqTask? currentTask)
    {
        ModellingHistoryEntry? creation = entries.FirstOrDefault(entry => entry.ChangeType == (int)ModellingTypes.ChangeType.Insert);
        object? currentData = currentTask == null ? null : RequestTaskSnapshot(currentTask);
        if (creation?.NewData == null || JToken.DeepEquals(ToJsonToken(creation.NewData), ToJsonToken(currentData)))
        {
            return null;
        }

        return new RequestTaskDiffResponse
        {
            RequestTaskId = entries.Key,
            Original = ToJsonElement(creation.NewData),
            Current = currentData == null ? null : ToJsonElement(currentData)
        };
    }

    /// <summary>
    /// Selects the request-task fields captured by workflow change history from the current database state.
    /// </summary>
    private static object RequestTaskSnapshot(WfReqTask task)
    {
        return new
        {
            task.Title,
            task.TaskType,
            task.RequestAction,
            task.RuleAction,
            task.Tracking,
            task.Start,
            task.Stop,
            task.FreeText,
            task.Reason,
            task.AdditionalInfo,
            task.ManagementId,
            task.SelectedDevices,
            Owners = task.Owners.Select(owner => owner.Owner.Id),
            Elements = task.Elements.Select(element => new
            {
                element.Id,
                element.Field,
                element.RequestAction,
                element.IpString,
                element.IpEnd,
                element.Port,
                element.PortEnd,
                element.ProtoId,
                element.NetworkId,
                element.ServiceId,
                element.Name,
                element.GroupName
            })
        };
    }

    /// <summary>
    /// Projects all audit-proof-critical implementation-task history entries as manual changes.
    /// </summary>
    /// <param name="history">Chronologically ordered workflow task history for one ticket.</param>
    /// <returns>The recorded manual implementation-task changes.</returns>
    private static List<ManualImplementationTaskChangeResponse> BuildManualImplementationTaskChanges(List<ModellingHistoryEntry> history)
    {
        return history.Where(entry => entry.ObjectType == (int)ChangeHistoryObjectType.ImplementationTask && entry.AuditProofCritical)
            .Select(entry => new ManualImplementationTaskChangeResponse
            {
                ImplementationTaskId = entry.ObjectId,
                ChangeTime = NormalizeStoredTime(entry.ChangeTime),
                ChangeUserId = entry.ChangerId,
                ChangeUserName = entry.Changer ?? string.Empty,
                Original = entry.OldData == null ? null : ToJsonElement(entry.OldData),
                Current = entry.NewData == null ? null : ToJsonElement(entry.NewData)
            })
            .ToList();
    }

    /// <summary>
    /// Converts the Newtonsoft value returned for a jsonb field to the response serializer's JSON type.
    /// </summary>
    /// <param name="value">A non-null jsonb value from the API response.</param>
    /// <returns>An independent JSON element preserving the stored snapshot.</returns>
    private static System.Text.Json.JsonElement ToJsonElement(object value)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(ToJsonToken(value).ToString(Formatting.None));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Normalizes a jsonb value to a token so snapshots can be compared structurally.
    /// </summary>
    /// <param name="value">A jsonb value returned by Newtonsoft or created by a test fixture.</param>
    /// <returns>The equivalent JSON token.</returns>
    private static JToken ToJsonToken(object? value)
    {
        return value as JToken ?? JToken.FromObject(value!);
    }

    /// <summary>
    /// Determines whether a workflow ticket with the supplied id exists.
    /// </summary>
    /// <param name="ticketId">Database id of the workflow ticket.</param>
    /// <returns>True when the ticket exists, otherwise false.</returns>
    private async Task<bool> TicketExistsAsync(long ticketId)
    {
        List<WfTicketBase> tickets = await apiConnection.SendQueryAsync<List<WfTicketBase>>(
            RequestQueries.getTicketIdIfExists, new { ticketId });

        return tickets.Count > 0;
    }

    /// <summary>
    /// Projects a stored change history row onto the response contract.
    /// </summary>
    /// <remarks>
    /// changer and change_text are nullable columns, so the empty string of the response contract is
    /// substituted here rather than letting a null reach a property declared as non-nullable.
    /// </remarks>
    private static AuditProofCriticalChangeResponse Map(ModellingHistoryEntry entry)
    {
        return new AuditProofCriticalChangeResponse
        {
            ChangeTime = NormalizeStoredTime(entry.ChangeTime),
            ChangeUserName = entry.Changer ?? string.Empty,
            ChangeUserId = entry.ChangerId,
            ChangeContent = entry.ChangeText ?? string.Empty
        };
    }

    private static bool Matches(AuditProofCriticalChangeResponse change, AuditProofCriticalChangeFilter? filter)
    {
        if (filter == null)
        {
            return true;
        }

        return MatchesTime(change.ChangeTime, filter.ChangeTime)
            && MatchesText(change.ChangeUserName, filter.ChangeUserName)
            && MatchesText(change.ChangeContent, filter.ChangeContent);
    }

    /// <summary>
    /// Compares a stored timestamp against the filter value on the wall clock both sides describe.
    /// </summary>
    /// <remarks>
    /// The stored column is timezone-naive, so a direct comparison would depend on which spelling the
    /// caller happened to use: DateTime equality compares ticks and ignores the kind, while the
    /// request deserializer leaves a trailing Z unshifted but converts an explicit offset to local
    /// time. Both sides are therefore reduced to the same wall clock before they are compared.
    /// </remarks>
    private static bool MatchesTime(DateTime? value, DateTime? expected)
    {
        return expected == null || value == NormalizeFilterTime(expected.Value);
    }

    /// <summary>
    /// Reduces a filter timestamp to the wall clock the stored column uses.
    /// </summary>
    /// <param name="expected">Timestamp as bound from the request.</param>
    /// <returns>The same point in time expressed as an unspecified-kind local wall clock.</returns>
    private static DateTime NormalizeFilterTime(DateTime expected)
    {
        return expected.Kind switch
        {
            // A trailing Z keeps UTC ticks, so it has to be moved onto the local clock the column stores.
            DateTimeKind.Utc => DateTime.SpecifyKind(expected.ToLocalTime(), DateTimeKind.Unspecified),
            // An explicit offset was already converted to local time while binding.
            DateTimeKind.Local => DateTime.SpecifyKind(expected, DateTimeKind.Unspecified),
            // No offset given: taken as the wall clock of the installation, like the stored value.
            _ => expected
        };
    }

    /// <summary>
    /// Drops the kind of a stored timestamp so it cannot depend on how the row was deserialized.
    /// </summary>
    /// <param name="value">Timestamp as read from the database.</param>
    /// <returns>The same wall clock with an unspecified kind, or null.</returns>
    private static DateTime? NormalizeStoredTime(DateTime? value)
    {
        return value == null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
    }

    private static bool MatchesText(string value, string? expected)
    {
        return expected == null || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
