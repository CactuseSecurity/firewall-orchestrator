using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetNetGroupValidityRequestItem type.
/// </summary>
public sealed class GetNetGroupValidityRequestItem
{
    /// <summary>
    /// Gets the IpStart value.
    /// </summary>
    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    /// <summary>
    /// Gets the IpEnd value.
    /// </summary>
    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;
}
