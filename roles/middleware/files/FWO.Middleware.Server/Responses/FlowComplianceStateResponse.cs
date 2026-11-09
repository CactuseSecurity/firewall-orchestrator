using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the FlowComplianceStateResponse type.
/// </summary>
public sealed class FlowComplianceStateResponse
{
    /// <summary>
    /// Performs the new operation.
    /// </summary>
    [JsonPropertyName("policy")]
    public CompliancePolicyResponse Policy { get; set; } = new();

    /// <summary>
    /// Gets the Violations value.
    /// </summary>
    [JsonPropertyName("violations")]
    public List<ComplianceViolationResponse> Violations { get; set; } = [];

    /// <summary>
    /// Represents the CompliancePolicyResponse type.
    /// </summary>
    public sealed class CompliancePolicyResponse
    {
        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    /// <summary>
    /// Represents the ComplianceViolationResponse type.
    /// </summary>
    public sealed class ComplianceViolationResponse
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets the Type value.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
