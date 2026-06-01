using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class ServiceObjectIdResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }
}
