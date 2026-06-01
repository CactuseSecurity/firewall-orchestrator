using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class CreateRequestResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public int RequestId { get; set; }
}
