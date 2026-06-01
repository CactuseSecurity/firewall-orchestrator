using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class GetRequestStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
