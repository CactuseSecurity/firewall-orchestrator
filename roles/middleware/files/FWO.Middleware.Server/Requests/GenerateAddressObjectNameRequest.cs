using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GenerateAddressObjectNameRequest type.
/// </summary>
public sealed class GenerateAddressObjectNameRequest
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

    /// <summary>
    /// Gets the NetMask value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("netMask")]
    public int NetMask { get; set; }
}
