using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Modelling;
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
    /// <returns>The matching changes, empty when the ticket has none or does not exist.</returns>
    /// <remarks>
    /// The ticket restriction is applied by the GraphQL query, the response filter in memory: the
    /// result set of a single ticket is small, and matching here keeps the filter semantics of the
    /// contract in one place instead of splitting them across query variables.
    /// </remarks>
    public async Task<GetAuditProofCriticalChangesResponse> GetAuditProofCriticalChangesAsync(long ticketId, AuditProofCriticalChangeFilter? filter)
    {
        List<ModellingHistoryEntry> entries = await apiConnection.SendQueryAsync<List<ModellingHistoryEntry>>(
            RequestQueries.getAuditProofCriticalChangesForTicket, new { ticketId });

        return new GetAuditProofCriticalChangesResponse
        {
            Changes = entries.Select(Map).Where(change => Matches(change, filter)).ToList()
        };
    }

    private static AuditProofCriticalChangeResponse Map(ModellingHistoryEntry entry)
    {
        return new AuditProofCriticalChangeResponse
        {
            ChangeTime = entry.ChangeTime,
            ChangeUserName = entry.Changer,
            ChangeContent = entry.ChangeText
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

    private static bool MatchesTime(DateTime? value, DateTime? expected)
    {
        return expected == null || value == expected;
    }

    private static bool MatchesText(string value, string? expected)
    {
        return expected == null || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
