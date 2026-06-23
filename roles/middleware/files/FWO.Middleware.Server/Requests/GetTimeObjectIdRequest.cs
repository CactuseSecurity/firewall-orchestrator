using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTimeObjectIdRequest type.
/// </summary>
public sealed class GetTimeObjectIdRequest : IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    /// <summary>
    /// Gets the StartTime value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets the EndTime value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("endTime")]
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
