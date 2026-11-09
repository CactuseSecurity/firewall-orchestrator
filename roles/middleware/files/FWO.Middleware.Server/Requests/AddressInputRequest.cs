using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents one IP address block of a request as exactly one of a single host (<c>ipHost</c>),
/// a CIDR network (<c>ipNetwork</c>) or an inclusive range (<c>ipRange</c>). IPv4 and IPv6 are accepted.
/// Validated and normalized by <see cref="AddressInputNormalizer"/>.
/// </summary>
public sealed class AddressInputRequest : IRequestWithAdditionalData, IAddressInput
{
    /// <summary>
    /// Gets or sets one IPv4 or IPv6 address without a mask, for example <c>10.1.1.5</c>.
    /// Mutually exclusive with <c>ipNetwork</c> and <c>ipRange</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipHost")]
    public string IpHost { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets one IPv4 or IPv6 network in CIDR notation, for example <c>10.1.0.0/24</c>.
    /// Host bits are cleared, so <c>10.1.1.5/24</c> covers <c>10.1.1.0</c> to <c>10.1.1.255</c>.
    /// Mutually exclusive with <c>ipHost</c> and <c>ipRange</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipNetwork")]
    public string IpNetwork { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an inclusive range as exactly two addresses without a mask, first the lower, then the
    /// upper bound, both of the same address family. Mutually exclusive with <c>ipHost</c> and
    /// <c>ipNetwork</c>. Null or empty means not given.
    /// </summary>
    [JsonPropertyName("ipRange")]
    public List<string>? IpRange { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
