using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents one IP address block of a request, given either as an inclusive range
/// or as a CIDR network, never both. IPv4 and IPv6 are accepted.
/// </summary>
public sealed class IpRangeOrNetworkRequest : IRequestWithAdditionalData
{
    /// <summary>
    /// The first address of an inclusive range. Must be given together with <c>ipEnd</c>
    /// and is mutually exclusive with <c>ipNetwork</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipStart")]
    public string? IpStart { get; set; }

    /// <summary>
    /// The last address of an inclusive range. Must be given together with <c>ipStart</c>
    /// and is mutually exclusive with <c>ipNetwork</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipEnd")]
    public string? IpEnd { get; set; }

    /// <summary>
    /// A network in CIDR notation, for example <c>10.1.0.0/24</c>. Mutually exclusive
    /// with <c>ipStart</c> and <c>ipEnd</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipNetwork")]
    public string? IpNetwork { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}