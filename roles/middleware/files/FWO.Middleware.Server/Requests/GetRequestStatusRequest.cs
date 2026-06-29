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
    [JsonRequired]
    [JsonPropertyName("ticketId")]
    public long TicketId { get; set; }
}
