using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class AddressGroupResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<AddressGroupMemberResponse> Members { get; set; } = [];

    public sealed class AddressGroupMemberResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
