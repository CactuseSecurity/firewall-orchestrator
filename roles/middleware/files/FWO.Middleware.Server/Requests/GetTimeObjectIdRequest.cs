using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTimeObjectIdRequest type.
/// </summary>
public sealed class GetTimeObjectIdRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the StartTime value.
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets the EndTime value.
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }

}
