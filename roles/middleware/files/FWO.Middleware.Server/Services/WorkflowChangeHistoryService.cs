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
