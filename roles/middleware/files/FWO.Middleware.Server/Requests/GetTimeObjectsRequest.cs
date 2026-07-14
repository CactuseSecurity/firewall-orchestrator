using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTimeObjectsRequest type.
/// </summary>
public sealed class GetTimeObjectsRequest : IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
