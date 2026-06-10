using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetRequestStatusRequest type.
/// </summary>
public sealed class GetRequestStatusRequest
{
    /// <summary>
    /// Gets the TicketId value.
    /// </summary>
    [JsonPropertyName("ticketId")]
    public int TicketId { get; set; }
}
