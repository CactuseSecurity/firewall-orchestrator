using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GenerateServiceObjectNameRequest type.
/// </summary>
public sealed class GenerateServiceObjectNameRequest
{
    /// <summary>
    /// Gets the PortStart value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("portStart")]
    public int PortStart { get; set; }

    /// <summary>
    /// Gets the PortEnd value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("portEnd")]
    public int PortEnd { get; set; }

    /// <summary>
    /// Gets the Protocol value.
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Typ value.
    /// </summary>
    [JsonPropertyName("typ")]
    public string Typ { get; set; } = string.Empty;
}
