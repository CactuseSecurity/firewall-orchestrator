using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the TimeObjectResponse type.
/// </summary>
public sealed class TimeObjectResponse
{
    /// <summary>
    /// Gets the Id value.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets the Name value. For time objects created by the request workflow the name shows the period as it
    /// was requested, in the local time of the request, while StartTime and EndTime are in UTC. With a time
    /// zone offset the name can therefore state a different day than the timestamps.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the StartTime value in UTC. This is the authoritative start of the period, see Name.
    /// </summary>
    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets the EndTime value in UTC. This is the authoritative end of the period, see Name.
    /// </summary>
    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = string.Empty;

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
