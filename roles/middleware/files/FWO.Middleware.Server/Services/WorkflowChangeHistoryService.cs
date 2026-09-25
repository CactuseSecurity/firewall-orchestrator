using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;

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

        return new GetAuditProofCriticalChangesResponse
        {
            Changes = entries.Select(Map).Where(change => Matches(change, filter)).ToList()
        };
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
