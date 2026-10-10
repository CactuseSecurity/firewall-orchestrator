using FWO.Services.Workflow;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the CreateTicketResponse type.
/// </summary>
public sealed class CreateTicketResponse
{
    /// <summary>
    /// Gets the Status value.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets the TicketId value.
    /// </summary>
    [JsonPropertyName("ticketId")]
    public long TicketId { get; set; }

    /// <summary>
    /// Gets the status of the initial workflow actions of the new ticket: <c>completed</c> if they all ran, or
    /// <c>failed</c> if the ticket was saved but at least one of them failed, so that e.g. notifications or
    /// assignments of its initial state may be missing. A failure is raised as alert for the administrators and
    /// recorded in the ticket's change history. Do not resend the request: it would create a second ticket.
    /// </summary>
    [JsonPropertyName("actionsStatus")]
    public string ActionsStatus { get; set; } = WfTicketCreationResult.kActionsCompleted;
}
