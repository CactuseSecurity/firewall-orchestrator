using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetFlowComplianceStateRequest : IRequestWithRootAdditionalData
{
    [JsonPropertyName("source")]
    public List<IpRangeRequest> Source { get; set; } = [];

    [JsonPropertyName("destination")]
    public List<IpRangeRequest> Destination { get; set; } = [];

    [JsonPropertyName("service")]
    public List<ServiceRangeRequest> Service { get; set; } = [];

    [JsonPropertyName("policies")]
    public List<int> Policies { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public sealed class IpRangeRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    public sealed class ServiceRangeRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("portStart")]
        public int PortStart { get; set; }

        [JsonPropertyName("portEnd")]
        public int PortEnd { get; set; }

        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
