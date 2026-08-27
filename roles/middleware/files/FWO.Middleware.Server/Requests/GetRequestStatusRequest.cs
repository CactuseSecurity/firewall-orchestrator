using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetRequestStatusRequest type.
/// </summary>
public sealed class GetRequestStatusRequest : RequestDto<RequestOptionsDto>
{
    /// <summary>
    /// Gets the TicketId value.
    /// </summary>
    [JsonPropertyName("ticketId")]
    public long? TicketId { get; set; }
}
