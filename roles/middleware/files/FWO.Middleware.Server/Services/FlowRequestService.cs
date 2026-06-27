using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Data;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides request workflow data for flow request REST endpoints.
/// </summary>
public sealed class FlowRequestService
{
    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowRequestService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Returns the workflow ticket status and latest ticket comment.
    /// </summary>
    public async Task<GetRequestStatusResponse?> GetRequestStatusAsync(long ticketId)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        WfStateDict states = await GetStateDictAsync();
        string status = states.GetName(ticket.StateId);
        ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
        if (extStateResponse.HasErrors || extStateResponse.Result == null)
        {
            throw new InvalidOperationException("Could not fetch external workflow states.");
        }

        string? mappedStatus = ExtStateHandler.GetPreferredExternalStateName(extStateResponse.Result, ticket.StateId, true);
        if (!string.IsNullOrWhiteSpace(mappedStatus))
        {
            status = mappedStatus;
        }

        return new GetRequestStatusResponse
        {
            Status = status,
            StatusComment = GetLatestTicketComment(ticket)
        };
    }

    /// <summary>
    /// Loads workflow state names.
    /// </summary>
    private async Task<WfStateDict> GetStateDictAsync()
    {
        WfStateDict loadedStateDict = new();
        await loadedStateDict.Init(apiConnection);
        return loadedStateDict;
    }

    /// <summary>
    /// Returns the newest non-empty ticket-level comment text.
    /// </summary>
    private static string GetLatestTicketComment(WfTicket ticket)
    {
        return ticket.Comments?
            .Where(comment => comment?.Comment != null && !string.IsNullOrWhiteSpace(comment.Comment.CommentText))
            .OrderByDescending(comment => comment!.Comment.CreationDate)
            .Select(comment => comment!.Comment.CommentText)
            .FirstOrDefault() ?? string.Empty;
    }
}
