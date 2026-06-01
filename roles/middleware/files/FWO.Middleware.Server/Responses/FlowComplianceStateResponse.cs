using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

public sealed class FlowComplianceStateResponse
{
    [JsonPropertyName("policy")]
    public CompliancePolicyResponse Policy { get; set; } = new();

    [JsonPropertyName("violations")]
    public List<ComplianceViolationResponse> Violations { get; set; } = [];

    public sealed class CompliancePolicyResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public sealed class ComplianceViolationResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
