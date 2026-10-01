using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTicketStatusRequest type.
/// </summary>
public sealed class GetTicketStatusRequest
{
    /// <summary>
    /// Gets the TicketId value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("ticketId")]
    public long TicketId { get; set; }
}
