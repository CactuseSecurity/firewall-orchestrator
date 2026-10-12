using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request for resolving explicitly referenced Flow groups.
/// </summary>
public sealed class ResolveFlowGroupsRequest : IRequestWithRootAdditionalData
{
    /// <summary>Gets or sets requested network group IDs.</summary>
    public List<long> NetworkGroupIds { get; set; } = [];
    /// <summary>Gets or sets requested network group names.</summary>
    public List<string> NetworkGroupNames { get; set; } = [];
    /// <summary>Gets or sets requested service group IDs.</summary>
    public List<long> ServiceGroupIds { get; set; } = [];
    /// <summary>Gets or sets requested service group names.</summary>
    public List<string> ServiceGroupNames { get; set; } = [];
    /// <summary>Gets unknown request fields for schema validation.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
