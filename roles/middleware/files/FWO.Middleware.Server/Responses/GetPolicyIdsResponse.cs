using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetPolicyIdsResponse type.
/// </summary>
public sealed class GetPolicyIdsResponse
{
    /// <summary>
    /// Gets the Policies value.
    /// </summary>
    [JsonPropertyName("policies")]
    public List<PolicyIdResponse> Policies { get; set; } = [];
}
