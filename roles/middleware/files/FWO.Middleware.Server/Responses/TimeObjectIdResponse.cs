using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the TimeObjectIdResponse type.
/// </summary>
public sealed class TimeObjectIdResponse
{
    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Id value.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
