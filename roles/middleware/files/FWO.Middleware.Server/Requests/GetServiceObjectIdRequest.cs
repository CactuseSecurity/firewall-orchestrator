using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetServiceObjectIdRequest type.
/// </summary>
public sealed class GetServiceObjectIdRequest : IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

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
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
