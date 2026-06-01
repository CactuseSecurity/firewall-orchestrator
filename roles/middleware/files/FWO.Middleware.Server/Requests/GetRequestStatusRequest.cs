using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetRequestStatusRequest
{
    [JsonPropertyName("ticketId")]
    public int TicketId { get; set; }
}
