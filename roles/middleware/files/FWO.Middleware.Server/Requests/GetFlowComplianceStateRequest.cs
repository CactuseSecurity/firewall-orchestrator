using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class GetFlowComplianceStateRequest
{
    [JsonPropertyName("source")]
    public List<IpRangeRequest> Source { get; set; } = [];

    [JsonPropertyName("destination")]
    public List<IpRangeRequest> Destination { get; set; } = [];

    [JsonPropertyName("service")]
    public List<ServiceRangeRequest> Service { get; set; } = [];

    [JsonPropertyName("policies")]
    public List<int> Policies { get; set; } = [];

    public sealed class IpRangeRequest
    {
        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;
    }

    public sealed class ServiceRangeRequest
    {
        [JsonPropertyName("portStart")]
        public int PortStart { get; set; }

        [JsonPropertyName("portEnd")]
        public int PortEnd { get; set; }

        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;
    }
}
