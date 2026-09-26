using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides workflow change history data for the workflow REST endpoints.
/// </summary>
public sealed class WorkflowChangeHistoryService
{
    /// <summary>
    /// Serializer with the settings the GraphQL client uses to write change-history snapshots.
    /// </summary>
    /// <remarks>
    /// Snapshots reach change_history.new_data as GraphQL variables, so their keys are camelCased by
    /// the client's contract resolver. A current state converted with any other settings would never
    /// compare equal to the stored snapshot, nor match its key names in the response.
    /// </remarks>
    private static readonly JsonSerializer kHistorySnapshotSerializer = JsonSerializer.Create(NewtonsoftJsonSerializer.DefaultJsonSerializerSettings);

    /// <summary>
    /// Stored request-task snapshot keys that workflow actions write, not the requester or an editor.
    /// </summary>
    /// <remarks>
    /// Start and stop follow the state transitions of the task, additional info holds bookkeeping keys
    /// of workflow actions. Neither is request content, so they are left out when deciding whether a
    /// request task differs from what was requested. The names are the camelCased keys the history
    /// serializer writes.
    /// </remarks>
    private static readonly List<string> kNonContentRequestTaskFields = new() { "start", "stop", "additionalInfo" };

    /// <summary>
    /// Stored request-task snapshot keys whose arrays carry no meaningful order.
    /// </summary>
    private static readonly List<string> kUnorderedRequestTaskFields = new() { "owners", "elements" };

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
            : await GetTaskDiffAsync(ticketId, filter);

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
    /// <param name="filter">Response filter, applied to the manual implementation-task changes.</param>
    /// <returns>The request-task diff and the manual implementation-task history entries.</returns>
    private async Task<AuditProofTaskDiffResponse> GetTaskDiffAsync(long ticketId, AuditProofCriticalChangeFilter? filter)
    {
        List<ModellingHistoryEntry> history = await apiConnection.SendQueryAsync<List<ModellingHistoryEntry>>(
            RequestQueries.getWorkflowTaskHistoryForTicket, new { ticketId });
        WfTicket currentTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });

        return new AuditProofTaskDiffResponse
        {
            RequestTaskDiffs = BuildRequestTaskDiffs(history, currentTicket.Tasks),
            ManualImplementationTaskChanges = BuildManualImplementationTaskChanges(history, filter)
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
        if (creation?.NewData == null)
        {
            return null;
        }

        JToken? currentData = currentTask == null ? null : ToJsonToken(WfDbAccess.RequestTaskHistorySnapshot(currentTask));
        if (currentData != null && JToken.DeepEquals(RequestContentOf(ToJsonToken(creation.NewData)), RequestContentOf(currentData)))
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
    /// Reduces a stored request-task snapshot to the request content that is compared.
    /// </summary>
    /// <param name="snapshot">A request-task snapshot in the stored shape.</param>
    /// <returns>A copy without the workflow-written fields and with unordered arrays in a fixed order.</returns>
    private static JToken RequestContentOf(JToken snapshot)
    {
        if (snapshot is not JObject stored)
        {
            return snapshot;
        }

        JObject content = (JObject)stored.DeepClone();
        foreach (string field in kNonContentRequestTaskFields)
        {
            content.Remove(field);
        }
        foreach (string field in kUnorderedRequestTaskFields)
        {
            if (content[field] is JArray items)
            {
                content[field] = new JArray(items.OrderBy(item => item.ToString(Formatting.None), StringComparer.Ordinal));
            }
        }
        return content;
    }

    /// <summary>
    /// Projects the audit-proof-critical implementation-task history entries that match the filter as manual changes.
    /// </summary>
    /// <param name="history">Chronologically ordered workflow task history for one ticket.</param>
    /// <param name="filter">Response filter; null applies no restriction.</param>
    /// <returns>The recorded manual implementation-task changes.</returns>
    private static List<ManualImplementationTaskChangeResponse> BuildManualImplementationTaskChanges(List<ModellingHistoryEntry> history,
        AuditProofCriticalChangeFilter? filter)
    {
        return history.Where(entry => entry.ObjectType == (int)ChangeHistoryObjectType.ImplementationTask && entry.AuditProofCritical)
            .Where(entry => Matches(Map(entry), filter))
            .Select(entry => new ManualImplementationTaskChangeResponse
            {
                ImplementationTaskId = entry.ObjectId,
                ChangeTime = WallClockTimestamp.NormalizeStored(entry.ChangeTime),
                ChangeUserId = entry.ChangerId,
                ChangeUserName = entry.Changer ?? string.Empty,
                ChangeContent = entry.ChangeText ?? string.Empty,
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
    /// <param name="value">A non-null jsonb value returned by Newtonsoft, or a snapshot object that is
    /// serialized the way the GraphQL client stores it.</param>
    /// <returns>The equivalent JSON token.</returns>
    private static JToken ToJsonToken(object value)
    {
        return value as JToken ?? JToken.FromObject(value, kHistorySnapshotSerializer);
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
            ChangeTime = WallClockTimestamp.NormalizeStored(entry.ChangeTime),
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

        return WallClockTimestamp.Matches(change.ChangeTime, filter.ChangeTime)
            && MatchesText(change.ChangeUserName, filter.ChangeUserName)
            && MatchesText(change.ChangeContent, filter.ChangeContent);
    }

    private static bool MatchesText(string value, string? expected)
    {
        return expected == null || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
