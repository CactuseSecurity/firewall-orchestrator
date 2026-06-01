using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class ServiceGroupResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ServiceGroupMemberResponse> Members { get; set; } = [];

    public sealed class ServiceGroupMemberResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
