using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressObjectIdRequest type.
/// </summary>
public sealed class GetAddressObjectIdRequest : IVisibleInRequestFilterRequest, IAddressInput
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    /// <summary>
    /// Gets one maskless IPv4 or IPv6 address.
    /// </summary>
    [JsonPropertyName("ipHost")]
    public string IpHost { get; set; } = string.Empty;

    /// <summary>
    /// Gets one canonical IPv4 or IPv6 CIDR network.
    /// </summary>
    [JsonPropertyName("ipNetwork")]
    public string IpNetwork { get; set; } = string.Empty;

    /// <summary>
    /// Gets an inclusive range containing exactly two maskless addresses.
    /// </summary>
    [JsonPropertyName("ipRange")]
    public List<string>? IpRange { get; set; }

    internal string NormalizedIpStart { get; set; } = string.Empty;

    internal string NormalizedIpEnd { get; set; } = string.Empty;

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
