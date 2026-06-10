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
    public int Id { get; set; }

    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

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
