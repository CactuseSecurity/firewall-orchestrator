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
}
