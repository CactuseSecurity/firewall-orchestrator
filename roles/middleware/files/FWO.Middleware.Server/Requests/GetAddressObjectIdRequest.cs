using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressObjectIdRequest type.
/// </summary>
public sealed class GetAddressObjectIdRequest : IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    /// <summary>
    /// Gets the inclusive range start. Supply this together with ipEnd, or supply ipNetwork instead.
    /// </summary>
    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    /// <summary>
    /// Gets the inclusive range end. Supply this together with ipStart, or supply ipNetwork instead.
    /// </summary>
    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    /// <summary>
    /// Gets a bare IPv4/IPv6 address or canonical CIDR network. This is mutually exclusive with ipStart and ipEnd.
    /// </summary>
    [JsonPropertyName("ipNetwork")]
    public string IpNetwork { get; set; } = string.Empty;

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
