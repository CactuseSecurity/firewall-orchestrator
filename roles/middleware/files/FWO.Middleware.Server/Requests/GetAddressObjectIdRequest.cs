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
    /// Gets the IpStart value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("ipStart")]
    public string IpStart { get; set; } = string.Empty;

    /// <summary>
    /// Gets the IpEnd value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("ipEnd")]
    public string IpEnd { get; set; } = string.Empty;

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
