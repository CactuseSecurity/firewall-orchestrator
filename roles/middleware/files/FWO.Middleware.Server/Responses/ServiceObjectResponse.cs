using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the ServiceObjectResponse type.
/// </summary>
public sealed class ServiceObjectResponse
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
    /// Gets the PortStart value.
    /// </summary>
    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    /// <summary>
    /// Gets the PortEnd value.
    /// </summary>
    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    /// <summary>
    /// Gets the Protocol value.
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

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
