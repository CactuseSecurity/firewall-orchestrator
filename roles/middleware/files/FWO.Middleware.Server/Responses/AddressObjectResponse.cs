using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the AddressObjectResponse type.
/// </summary>
public sealed class AddressObjectResponse
{
    /// <summary>
    /// Gets the Id value.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Type value calculated from the IP range.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets the inclusive IP range as its first and last maskless address, or an empty list for
    /// non-IP objects such as FQDNs.
    /// </summary>
    [JsonPropertyName("ipRange")]
    public List<string> IpRange { get; set; } = [];

    /// <summary>
    /// Gets the State value.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ShowInRequest value.
    /// </summary>
    [JsonPropertyName("showInRequest")]
    public bool ShowInRequest { get; set; }
}
