using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the VisibleInRequestFilter type.
/// </summary>
public sealed class VisibleInRequestFilter
{
    /// <summary>
    /// Gets the VisibleInRequest value.
    /// </summary>
    [JsonPropertyName("visibleInRequest")]
    public bool? VisibleInRequest { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
